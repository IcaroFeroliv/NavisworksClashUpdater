using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Autodesk.Navisworks.Api.Plugins;

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
                MessageBox.Show($"Ocorreu um erro ao processar:\n{ex.Message}\n\nStack: {ex.StackTrace}", "Erro Capturado", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }
        }

        private int ExecuteCore()
        {
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

            // NAVISWORKS 2022: a coleção de testes é acessada diretamente por TestsData.Tests
            if (testsData == null || testsData.Tests == null || testsData.Tests.Count == 0)
            {
                MessageBox.Show("Não há testes de colisão criados neste arquivo.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 0;
            }

            SavedItemCollection rootTestsCollection = testsData.Tests;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivos JSON (*.json)|*.json",
                Title = "Selecione o arquivo de Status de Clashes"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return 0;
            }

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

            foreach (var update in clashUpdates)
            {
                ClashResultStatus targetStatus;

                if (string.Equals(update.Action, "FalsePositive", StringComparison.OrdinalIgnoreCase))
                {
                    targetStatus = ClashResultStatus.Reviewed;
                }
                else if (string.Equals(update.Action, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    targetStatus = ClashResultStatus.Approved;
                }
                else
                {
                    continue;
                }

                ClashTest targetTest = FindClashTest(rootTestsCollection, update.TestName);

                if (targetTest != null)
                {
                    SavedItem targetResult = FindClashResult(targetTest.Children, update.ClashName);

                    // NAVISWORKS 2022: o tipo é ClashResult (não IClashResult)
                    if (targetResult is ClashResult clashResult)
                    {
                        if (clashResult.Status != ClashResultStatus.Resolved)
                        {
                            // NAVISWORKS 2022: apenas 2 parâmetros (sem Assignee)
                            testsData.TestsEditResultStatus(clashResult, targetStatus);
                            updatedCount++;
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
                else
                {
                    notFoundCount++;
                }
            }

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