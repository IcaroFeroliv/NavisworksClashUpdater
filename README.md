# Navisworks Clash Updater

Plugin para **Autodesk Navisworks Manage** que atualiza em lote o status dos clashes do Clash Detective a partir de um arquivo JSON externo.

Desenvolvido pela **IcaroFeroliv** para automatizar a revisão de interferências em projetos de compatibilização (BIM / MEP).

---

## Funcionalidades

- Lê um arquivo `.json` com a lista de clashes a atualizar.
- Localiza o teste (`TestName`) e o clash (`ClashName`) no Clash Detective, inclusive dentro de pastas/grupos.
- Aplica o status correspondente:
  | Action (JSON)   | Status no Navisworks |
  |-----------------|----------------------|
  | `FalsePositive` | Reviewed (Analisado) |
  | `Approved`      | Approved (Aprovado)  |
- Clashes já **Resolved** são ignorados para preservar o histórico.
- Ações desconhecidas no JSON são ignoradas.
- Exibe um relatório final com a quantidade de clashes atualizados e ignorados/não encontrados.

---

## Versões suportadas e branches

Cada versão do Navisworks possui uma API ligeiramente diferente, por isso o projeto é separado em branches:

| Branch            | Navisworks | Observações                                              |
|-------------------|------------|----------------------------------------------------------|
| `navisworks-2022` | 2022       | Versão original do plugin                                |
| `navisworks-2027` | 2027       | Adaptada para as mudanças da API do Navisworks 2027      |
| `main`            | -          | Documentação e referência geral                          |

Principais ajustes feitos na versão 2027:

- Acesso à coleção de testes via `testsData.Value.TestsRoot.Children`.
- `TestsEditResultStatus` agora exige um terceiro parâmetro (`new Assignee()`).
- Remoção do Newtonsoft.Json em favor do `JavaScriptSerializer` (nativo do .NET Framework), evitando conflito de versões de assemblies dentro do Navisworks.

---

## Formato do JSON

O arquivo deve ser uma lista de objetos com três campos de texto:

```json
[
  {
    "TestName": "Estrutural x Hidráulica",
    "ClashName": "Clash1",
    "Action": "FalsePositive"
  },
  {
    "TestName": "Estrutural x Hidráulica",
    "ClashName": "Clash2",
    "Action": "Approved"
  }
]
```

| Campo       | Descrição                                                        |
|-------------|------------------------------------------------------------------|
| `TestName`  | Nome do teste no Clash Detective (não diferencia maiúsculas).    |
| `ClashName` | Nome do clash dentro do teste (não diferencia maiúsculas).       |
| `Action`    | `FalsePositive` ou `Approved`.                                   |

---

## Requisitos

- Autodesk Navisworks Manage (2022 ou 2027, conforme a branch).
- Windows x64.
- Para compilar:
  - Visual Studio 2022 ou superior (ou .NET SDK).
  - .NET Framework 4.8 (target `net48`).
  - SDK/DLLs do Navisworks da versão correspondente.

---

## Compilação

1. Clone o repositório e mude para a branch da versão desejada:

   ```bash
   git clone [https://github.com/SEU-USUARIO/NavisworksClashUpdater.git](https://github.com/IcaroFeroliv/NavisworksClashUpdater)
   cd NavisworksClashUpdater
   git checkout navisworks-2027
   ```

2. Confira no `.csproj` se os caminhos das referências apontam para a sua instalação:

   ```xml
   <Reference Include="Autodesk.Navisworks.Api">
     <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2027\Autodesk.Navisworks.Api.dll</HintPath>
     <Private>False</Private>
   </Reference>
   <Reference Include="Autodesk.Navisworks.Clash">
     <HintPath>C:\Program Files\Autodesk\Navisworks Manage 2027\Autodesk.Navisworks.Clash.dll</HintPath>
     <Private>False</Private>
   </Reference>
   <Reference Include="System.Web.Extensions" />
   ```

3. Compile em **Release** (`dotnet build -c Release` ou pelo Visual Studio).

---

## Instalação

1. Crie uma pasta com o **mesmo nome da DLL** dentro de `Plugins` do Navisworks:

   ```
   C:\Program Files\Autodesk\Navisworks Manage 2027\Plugins\NavisworksClashUpdater\
   ```

2. Copie `NavisworksClashUpdater.dll` para essa pasta.
3. Abra o Navisworks. O botão **Atualizar Clashes via JSON** aparecerá na aba **Add-ins**.

> Para a versão 2022, use a pasta `Navisworks Manage 2022\Plugins\NavisworksClashUpdater\`.

---

## Como usar

1. Abra o modelo no Navisworks com os testes do Clash Detective já criados.
2. Vá em **Add-ins > Atualizar Clashes via JSON**.
3. Selecione o arquivo `.json`.
4. Aguarde o relatório final com o resultado do processamento.

---

## Solução de problemas

**`Could not load file or assembly 'Newtonsoft.Json'`**
O Navisworks não encontra a DLL ou há conflito de versão com a que ele já carrega. Este projeto resolve isso usando `System.Web.Script.Serialization.JavaScriptSerializer`, sem dependências externas. Se você adicionar bibliotecas de terceiros, copie as DLLs junto com a do plugin ou evite o conflito com bibliotecas nativas.

**O botão não aparece no Navisworks**
Verifique se a pasta tem o mesmo nome da DLL principal e se a DLL foi compilada em x64.

**Nenhum clash é atualizado**
Confira se `TestName` e `ClashName` no JSON são idênticos aos nomes exibidos no Clash Detective e se os clashes não estão com status *Resolved*.

---

## Contribuindo

1. Faça um fork do projeto.
2. Crie uma branch para sua alteração (`git checkout -b feature/minha-melhoria`).
3. Faça o commit (`git commit -m "Descrição da melhoria"`).
4. Envie para o repositório (`git push origin feature/minha-melhoria`).
5. Abra um Pull Request.

---

## Autor

IcaroFeroliv
