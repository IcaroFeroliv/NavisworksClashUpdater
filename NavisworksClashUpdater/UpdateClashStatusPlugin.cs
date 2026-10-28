using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Plugins;
using System.Web.Script.Serialization;

namespace NavisworksClashUpdater
{
    public class ClashUpdateModel
    {
        public string TestName { get; set; }
        public string ClashName { get; set; }
        public string Action { get; set; }
    }

    [Plugin("NavisworksClashUpdater", "FluxoMEP", DisplayName = "Atualizar Clashes via JSON", ToolTip = "Atualiza clashes (Reviewed/Approved) lendo um arquivo JSON externo.")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class UpdateClashStatusPlugin : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                return ExecuteCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocorreu um erro ao processar as bibliotecas:\n{ex.Message}\n\nStack: {ex.StackTrace}", "Erro Capturado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
        }

        private int ExecuteCore()
        {
            // Validações de segurança
            Document doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
            {
                MessageBox.Show("Nenhum modelo aberto no Navisworks.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            DocumentClash documentClash = doc.GetClash();
            if (documentClash == null)
            {
                MessageBox.Show("O módulo Clash Detective não está disponível.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            DocumentClashTests testsData = documentClash.TestsData;

            // CORREÇÃO 1 (Navisworks 2027): O acesso à coleção SavedItemCollection agora passa por Value.TestRoot.Children
            if (testsData == null || testsData.Value == null || testsData.Value.TestsRoot == null || testsData.Value.TestsRoot.Children.Count == 0)
            {
                MessageBox.Show("Não há testes de colisão criados neste arquivo.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            // Guardamos a coleção raiz em uma variável para facilitar o uso abaixo
            SavedItemCollection rootTestsCollection = testsData.Value.TestsRoot.Children;

            // Seleção do Arquivo
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos JSON (*.json)|*.json",
                Title = "Selecione o arquivo de Status de Clashes"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return 0;
            }

            // Leitura nativa do JSON no .NET moderno
            string jsonContent = File.ReadAllText(openFileDialog.FileName);

            var serializer = new JavaScriptSerializer();
            List<ClashUpdateModel> clashUpdates =
                serializer.Deserialize<List<ClashUpdateModel>>(jsonContent);

            if (clashUpdates == null || clashUpdates.Count == 0)
            {
                MessageBox.Show("O arquivo JSON está vazio ou inválido.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            int updatedCount = 0;
            int notFoundCount = 0;

            // Processamento Híbrido (Reviewed e Approved)
            foreach (var update in clashUpdates)
            {
                // 1. Define qual será o status a ser aplicado com base na string do JSON
                ClashResultStatus targetStatus;

                if (string.Equals(update.Action, "FalsePositive", StringComparison.OrdinalIgnoreCase))
                {
                    targetStatus = ClashResultStatus.Reviewed; // Falso Positivo vira Analisado (Reviewed)
                }
                else if (string.Equals(update.Action, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    targetStatus = ClashResultStatus.Approved; // Aprovado vira Aprovado (Approved)
                }
                else
                {
                    // Se houver alguma ação desconhecida no JSON, ele pula para o próximo
                    continue;
                }

                // 2. CORREÇÃO 2: Passa a coleção rootTestsCollection (testsData.Value.TestRoot.Children)
                ClashTest targetTest = FindClashTest(rootTestsCollection, update.TestName);

                if (targetTest != null)
                {
                    SavedItem targetResult = FindClashResult(targetTest.Children, update.ClashName);

                    if (targetResult is IClashResult clashResult)
                    {
                        // 3. BARREIRA LÓGICA: Verifica se o clash NÃO está resolvido antes de aplicar
                        if (clashResult.Status != ClashResultStatus.Resolved)
                        {
                            // CORREÇÃO 3 (CS7036): Passando new Assignee() como terceiro parâmetro obrigatório no 2027
                            testsData.TestsEditResultStatus(clashResult, targetStatus, new Assignee());
                            updatedCount++;
                        }
                        else
                        {
                            // Como já está Resolved, o plugin não altera o status para não quebrar o histórico.
                            notFoundCount++;
                        }
                    }
                    else
                    {
                        notFoundCount++;
                    }
                }
                else
                {
                    notFoundCount++;
                }
            }

            // Relatório Final
            string resultMessage = $"Processamento concluído!\n\nClashes atualizados com sucesso: {updatedCount}";
            if (notFoundCount > 0)
            {
                resultMessage += $"\nClashes ou Testes não encontrados / já resolvidos (ignorados): {notFoundCount}";
            }

            MessageBox.Show(resultMessage, "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private ClashTest FindClashTest(SavedItemCollection items, string testName)
        {
            foreach (SavedItem item in items)
            {
                if (item is ClashTest test && test.DisplayName.Equals(testName, StringComparison.OrdinalIgnoreCase))
                {
                    return test;
                }

                if (item is GroupItem group)
                {
                    ClashTest found = FindClashTest(group.Children, testName);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        private SavedItem FindClashResult(SavedItemCollection items, string clashName)
        {
            foreach (SavedItem item in items)
            {
                if (item.DisplayName.Equals(clashName, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                if (item is GroupItem group)
                {
                    SavedItem found = FindClashResult(group.Children, clashName);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}