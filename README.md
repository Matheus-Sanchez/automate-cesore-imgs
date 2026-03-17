# FaceCensorApp

Aplicação desktop em **C# + WinForms** para **censura automática de rostos em imagens**, com pré-visualização, ajuste manual de máscaras e processamento em lote.

## Sobre o projeto

O **FaceCensorApp** foi criado para facilitar a censura de rostos em grandes quantidades de imagens de forma simples e prática.

A aplicação permite:
- selecionar uma pasta raiz com imagens;
- detectar rostos automaticamente com um modelo **ONNX**;
- escolher o tipo de censura;
- revisar e ajustar máscaras manualmente antes do processamento;
- processar um lote completo;
- salvar a saída organizada em pastas, com logs e resumo da execução.

## Pasta de exemplo para teste de um dataset publico
https://senacspedu-my.sharepoint.com/:u:/g/personal/matheus_sduda_senacsp_edu_br/IQCIV8bn_Zi-T4akQ22jaEfgAXvq4H5ItlYHLj-qz_m6M2g?e=8axPVJ

-link do dataset: https://www.kaggle.com/datasets/fareselmenshawii/face-detection-dataset?resource=download-directory

## Como ele funciona

O fluxo principal do sistema é este:
1. O usuário abre o programa.
2. Seleciona a **pasta raiz** onde estão as imagens.
3. O sistema faz uma varredura dos arquivos suportados.
4. O usuário escolhe o **modelo ONNX** de detecção facial.
5. O programa carrega uma imagem de amostra e mostra:
   - as máscaras editáveis;
   - a prévia do resultado censurado.
6. O usuário configura o filtro desejado:
   - círculo preto;
   - retângulo sólido;
   - pixelização;
   - desfoque simples.
7. Se necessário, o usuário pode:
   - adicionar máscaras manualmente;
   - remover máscaras;
   - revisar detecções com baixa confiança.
8. Ao clicar em **Processar lote**, o sistema aplica a censura em todas as imagens encontradas.
9. No final, a aplicação salva:
   - imagens censuradas;
   - cópias dos originais, se configurado;
   - logs da execução;
   - um arquivo `summary.json` com o resumo do processamento.

## Arquivos suportados

### Imagens processadas
- `.jpg`
- `.jpeg`
- `.png`
- `.bmp`

### Vídeos encontrados na pasta
- `.mp4`
- `.avi`
- `.mov`
- `.mkv`

> **Observação:** nesta versão, os vídeos são identificados na varredura, mas ainda são **ignorados no processamento**.

## Pastas ignoradas automaticamente

Durante a varredura, o sistema ignora diretórios com estes nomes:
- `Saida`
- `Originais`
- `Censurados`
- `logs`

Isso evita reprocessar arquivos já gerados pelo próprio programa.

## Estrutura do projeto

```text
FaceCensorApp.sln
├── FaceCensorApp.WinForms         # Interface desktop
├── FaceCensorApp.Application      # Regras de aplicação / processamento do lote
├── FaceCensorApp.Domain           # Modelos e enums de domínio
├── FaceCensorApp.Infrastructure   # Scanner, saída, logs, settings
├── FaceCensorApp.AI               # Detector facial com ONNX
└── FaceCensorApp.Tests            # Testes automatizados
```

## Como executar para desenvolvimento

### Requisitos
- Windows
- .NET 8 SDK
- Visual Studio 2022 (recomendado) com suporte a .NET Desktop / WinForms

### Pelo Visual Studio
1. Clone o repositório ou baixe o código-fonte.
2. Abra o arquivo `FaceCensorApp.sln`.
3. Defina `FaceCensorApp.WinForms` como projeto de inicialização.
4. Compile a solução.
5. Execute com `F5` ou `Ctrl + F5`.

### Pelo terminal
Na raiz do projeto, execute:
```bash
dotnet restore
dotnet build FaceCensorApp.sln -c Release
dotnet run --project .\FaceCensorApp.WinForms\FaceCensorApp.WinForms.csproj
```

## Como gerar uma versão para usuário final

**Importante:** o ZIP do GitHub baixa apenas o código-fonte. Para um usuário final abrir com dois cliques, você deve publicar o aplicativo primeiro.

Use este comando na raiz do projeto:
```bash
dotnet publish .\FaceCensorApp.WinForms\FaceCensorApp.WinForms.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```
Depois disso, será criada uma pasta parecida com `publish/win-x64/`. Dentro dela você deverá encontrar o executável do programa e os arquivos necessários para rodar.

## Como distribuir para o usuário final

A forma mais simples é:
1. Gere a pasta publicada com `dotnet publish`.
2. Compacte a pasta `publish/win-x64` em um `.zip`.
3. Envie esse `.zip` para o usuário.

**O usuário deverá:**
1. baixar o ZIP;
2. descompactar;
3. abrir a pasta extraída;
4. clicar duas vezes no executável do aplicativo.

### Exemplo de passo a passo para o usuário final
1. Baixe o arquivo `FaceCensorApp-win-x64.zip`
2. Descompacte o ZIP
3. Abra a pasta extraída
4. Clique duas vezes em `FaceCensorApp.WinForms.exe`
5. No programa:
   - selecione a pasta com as imagens;
   - ajuste o filtro;
   - clique em **Processar lote**.

## Saída gerada pelo programa

Quando o processamento é executado, o sistema cria uma estrutura parecida com esta:

```text
PastaRaiz/
└── Saida/
    └── 20260317-123456/
        ├── Censurados/
        ├── Originais/
        └── logs/
            ├── execution-log.txt
            └── summary.json
```

### Significado das pastas
- **Censurados/** → arquivos processados
- **Originais/** → backup dos arquivos originais, quando habilitado
- **logs/execution-log.txt** → log textual da execução
- **logs/summary.json** → resumo estruturado do processamento

## Configurações salvas automaticamente

A aplicação salva preferências do usuário, como:
- última pasta utilizada;
- último modelo ONNX selecionado;
- limiar de confiança;
- preset de censura.

Essas configurações ficam salvas em:
`%LocalAppData%\FaceCensorApp\settings.json`

## Filtros disponíveis

- Círculo preto
- Retângulo sólido
- Pixelização
- Desfoque simples

Também é possível ajustar:
- confiança;
- margem;
- intensidade de blur;
- tamanho do pixel;
- opacidade;
- cor do filtro, quando aplicável.

## Recursos da interface

A interface principal possui:
- seleção de pasta raiz;
- seleção de modelo ONNX;
- lista de imagens encontradas;
- painel de máscaras editáveis;
- painel de pré-visualização do resultado;
- barra de progresso;
- resumo da execução;
- botão para abrir a pasta de saída.

## Limitações atuais

- processamento de vídeos ainda não está ativo nesta versão;
- o sistema depende de um modelo ONNX válido para a detecção automática;
- a distribuição para usuário final ainda não está empacotada em instalador.

## Tecnologias usadas

- C#
- .NET 8
- Windows Forms
- ONNX Runtime

## Ideias de melhorias futuras

- criar instalador `.msi` ou `.exe`
- publicar releases no GitHub
- adicionar suporte real a vídeos
- melhorar identidade visual
- permitir exportação de presets
- adicionar arrastar e soltar de pastas/arquivos
