# Plataforma DEI — fluxo do projeto e base de dados

Versão visual: página "Mapa da Plataforma DEI" (claude.ai). Os diagramas abaixo são os mesmos, em Mermaid.

## Fluxo do projeto

Cada área (DEP-INF, DEP-PRCS, DEP-PRM, DEP-SAF) propõe os seus projetos no plano do ano. Só os projetos **aceites** formam o Capex e só esses podem ter compras (Pedido → Processo) ou obras (Projeto de Obra). O adjudicado volta ao projeto como execução e, no fim do ano, decide o que transita.

```mermaid
flowchart TD
    A["Áreas propõem projetos<br/>(registo ou ficheiro SCOPE por área)"] --> B["Plano Capex do ano<br/>todos os projetos introduzidos"]
    B --> C{"Decisão"}
    C -- Rejeitado --> R["Fica no plano como rejeitado<br/>não conta nos totais"]
    C -- Proposto --> B
    C -- Aceite --> D["Capex aprovado<br/>orçamento = valor a transitar + valor novo"]
    D --> E{"Tipo de execução"}
    E -- "Compra (equipamento, serviços)" --> F["Pedido de Proposta<br/>ligado ao projeto Capex"]
    F --> G["Processo<br/>critérios, propostas, avaliação"]
    G --> H{"Adjudicação<br/>valor > saldo do projeto?"}
    E -- "Obra (abrigo, construção civil, edifício)" --> I["Projeto de Obra<br/>ligado ao projeto Capex"]
    I --> J["Mapa de Quantidades<br/>propostas de empreiteiros, avaliação"]
    J --> K{"Adjudicação<br/>valor > saldo do projeto?"}
    H -- "não" --> X["Execução do Capex<br/>adjudicado · em curso · saldo"]
    H -- "sim" --> HJ["Justificação obrigatória"] --> X
    K -- "não" --> X
    K -- "sim" --> KJ["Justificação obrigatória"] --> X
    X --> Y["Preparar plano do ano seguinte<br/>revisão projeto a projeto"]
    Y -- "Transitado / Transitado parcial<br/>(continua aceite)" --> N["Plano Capex ano + 1"]
    Y -- "Condicionado<br/>(volta a proposto)" --> N
    Y -- "Executado · Cancelado · Exploração DOM<br/>(fica rejeitado)" --> N
    N --> A
```

## Aprovação dos projetos

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Proposto
    Proposto --> Aceite: aceitar
    Proposto --> Rejeitado: rejeitar (motivo)
    Aceite --> Proposto: voltar a proposto
    Rejeitado --> Proposto: voltar a proposto
```

## Regras

- Pedidos de Proposta e Projetos de Obra novos têm de escolher um projeto Capex **aceite** (os registos antigos podem ficar sem projeto).
- Adjudicação (Processo ou Obra): se o adjudicado nas outras compras/obras do projeto + o valor a adjudicar ultrapassar o TOTAL ORC., a justificação é obrigatória.
- Execução = adjudicado (processos em CVE + obras) / em curso (orçamento estimado dos pedidos + valor estimado das obras sem adjudicação). Cancelados não contam.
- Projeto com compras ou obras não pode ser rejeitado nem apagado; recarregar o ficheiro de uma área só substitui os projetos dessa área.
- Plano do ano seguinte: valor a transitar sugerido = orçamento − adjudicado. Transitado/parcial → aceite; Condicionado → proposto; Executado/Cancelado/Exploração DOM → rejeitado.

## Base de dados

```mermaid
erDiagram
    Planos ||--o{ ProjetosPlano : "tem"
    Planos ||--o{ AcoesFormacaoPlano : "tem"
    ProjetosPlano |o..o{ ProjetosPlano : "transitou de (ProjetoOrigemId)"
    ProjetosPlano |o..o{ Pedidos : "executado por compras"
    ProjetosPlano |o..o{ ProjetosObra : "executado por obras"

    Pedidos ||--o{ ItensPedido : "itens pedidos"
    ItensMaterial ||--o{ ItensPedido : "material"
    Pedidos ||--|| Processos : "gera 1 processo"
    Processos ||--o{ Criterios : "critérios"
    Processos ||--o{ Propostas : "propostas"
    Processos |o..o| Propostas : "vencedora"
    Propostas ||--o{ ItensProposta : "itens"
    ItensMaterial ||--o{ ItensProposta : "material"
    Propostas ||--o{ Avaliacoes : "avaliações"
    Criterios ||--o{ Avaliacoes : "por critério"
    Avaliadores ||--o{ Avaliacoes : "por avaliador"
    Propostas ||--o{ PropostasAnexo : "anexos"
    Propostas ||--o{ MemoriaCalculoAvaliacao : "cálculo automático"
    ItensMaterial |o..o{ ItensMaterial : "subitens"

    ProjetosObra ||--o{ ItensMQT : "mapa de quantidades"
    ProjetosObra ||--o{ CriteriosObra : "critérios"
    ProjetosObra ||--o{ PropostasEmpreiteiro : "propostas"
    ProjetosObra |o..o| PropostasEmpreiteiro : "vencedora"
    ProjetosObra ||--o{ ProjetoObraAnexos : "documentos"
    PropostasEmpreiteiro ||--o{ ItensPropostaEmpreiteiro : "preços"
    ItensMQT ||--o{ ItensPropostaEmpreiteiro : "item do MQT"
    PropostasEmpreiteiro ||--o{ AvaliacoesObra : "avaliações"
    CriteriosObra ||--o{ AvaliacoesObra : "por critério"

    Planos {
        int Id PK
        int Ano
        string Designacao
        string Departamento
        decimal CapacidadeArquiteturaDias
        decimal CapacidadeEnergiaDias
    }
    ProjetosPlano {
        int Id PK
        int PlanoId FK
        int ProjetoOrigemId FK "nulo = proposto de raiz"
        string Area "DepInf, DepPrcs, DepPrm, DepSaf"
        string EstadoAprovacao "Proposto, Aceite, Rejeitado"
        string Nome
        string DesignacaoRubrica
        string Classificacao
        string Ilha
        string Situacao "Transitado ... Exploracao DOM"
        decimal ValorTransitar
        decimal ValorNovo
        decimal ValorCondicionado
        decimal TotalOrcamentoAnoAnterior
        decimal CustoConstrucoes
        decimal EsforcoArquiteturaDias
        decimal EsforcoEnergiaDias
    }
    AcoesFormacaoPlano {
        int Id PK
        int PlanoId FK
        int Prioridade "1, 2, 3"
        string Tipo "Formacao, Feira"
        string Acao
        int NumeroPessoas
        bool NoPlano
        int Ano
    }
    Pedidos {
        int Id PK
        int ProjetoPlanoId FK "obrigatório nos novos"
        string TipoProposta
        string Area
        string Status "EmCurso, Finalizado, Cancelado"
        decimal OrcamentoEstimado
    }
    Processos {
        int Id PK
        int PedidoPropostaId FK
        string NumeroProcesso
        string Status "Criado, Recebido, Concluido, Cancelado"
        int PropostaVencedoraId FK
        decimal ValorAdjudicadoCVE
        string JustificativaAdjudicacao
    }
    ProjetosObra {
        int Id PK
        int ProjetoPlanoId FK "obrigatório nas novas"
        string Designacao
        string Status "EmConcurso, EmExecucao, Concluido"
        decimal ValorEstimado
        int PropostaVencedoraId FK
        decimal ValorAdjudicado
        string JustificativaAdjudicacao
    }
```

## Gestão de Stock

Materiais comprados pela DEI e guardados em armazém. O stock de cada material **não se guarda**: calcula-se pelos movimentos (entradas, saídas e ajustes de inventário), por **área DEP** (DEP-INF, DEP-PRCS, DEP-PRM, DEP-SAF) e por **armazém**.

```mermaid
flowchart TD
    PL["Packing List<br/>(Huawei ou outro fornecedor, contrato 1+1)"] --> E["Entrada de stock<br/>área + armazém"]
    CP["Compra DEI<br/>(Processo de compra da plataforma)"] --> E
    SAP["Ficheiro SAP do armazém<br/>todas as ilhas, um depósito por armazém"] --> INV["Um inventário por armazém<br/>(aberto para rever)"]
    INV -- "fechar" --> AJ["Ajuste: o stock do armazém<br/>passa a ser a contagem mais recente"]
    E --> S[("Stock por material<br/>área DEP × armazém × área técnica")]
    AJ --> S
    S --> SA["Saída<br/>(projeto Capex, site, nº de série)"]
    S --> AL["Alarmes<br/>saldo negativo · esgotado · abaixo do mínimo · parado · nome diferente · sem código SAP"]
```

### Armazéns (Centro / Depósito do SAP)

O ficheiro do SAP do armazém traz **todas as ilhas** no mesmo Excel (uma folha por ilha). Cada linha diz o **Centro** e o **Depósito**, e a coluna **Observações** dá a designação do armazém. A plataforma cria o armazém automaticamente na primeira vez que o depósito aparece (ex.: Santo Antão, São Nicolau, Maio ou Brava entram da mesma forma).

| Centro | Depósito | Armazém | Ilha | Entra no stock DEI |
|---|---|---|---|---|
| CPRA | DPRP | Praia_Projeto | Santiago | Sim — linhas DEI / AIRPON / ENERGIA (coluna Área) |
| CPRA | DSRP | SAL_Projeto | Sal | Sim — todo o armazém |
| CPRA | DBRP | Boavista_Projeto | Boa Vista | Sim — todo o armazém |
| CPRA | DFRP | Fogo_S.Filipe_Projeto | Fogo | Sim — todo o armazém |
| CMIN | DMRP | S.Vicente_Mindelo_Projeto | São Vicente | Sim — todo o armazém |
| CPRA | DBVS | Boavista_Técnico | Boa Vista | Não (técnico) |
| CPRA | DFIL | Fogo_S.Filipe_Técnico | Fogo | Não (técnico) |
| CMIN | DMIN | S.Vicente_Mindelo_Técnico | São Vicente | Não (técnico) |
| CMIN | DGRM | S.Vicente_D Gest Rede Mind | São Vicente | Não (técnico) |

Colunas lidas: `Centro`, `Depósito`, `Material` (código = ID do material), `TxtBreveMaterial` (descrição), `Contagem` / `Contagem Total`, `Utiliz.livre` (unidade), `Observações` (armazém) e `Área` (projeto, quando existe).

### Que materiais entram (DEI, AIRPON, ENERGIA) e área técnica

Na importação indica-se as áreas a trazer (por omissão `DEI, AIRPON, ENERGIA`):

- Folhas **com** a coluna Área: entra a linha cuja Área tem uma dessas palavras (DEI_FTTH, DEI_TORRE, AIRPON, ENERGIA_Projeto Power Mindelo, "RAM, ENERGIA"…).
- Folhas **sem** a coluna Área (SAL, Boavista, Fogo, S.Vicente…): os **armazéns de Projeto** (Observações com "Projeto": SAL_Projeto, Boavista_Projeto, Fogo_S.Filipe_Projeto, S.Vicente_Mindelo_Projeto…) **são da DEI e entram inteiros**; a área técnica vem do mesmo código noutra folha (ex.: DEI_FTTH na Praia) ou fica DEI (outros). Os armazéns **Técnico** e outros (Boavista_Técnico, Fogo_S.Filipe_Técnico, S.Vicente_Mindelo_Técnico, D Gest Rede) **não são DEI e ficam de fora**.

A área técnica fica no material (dentro da área DEP, ex.: DEP-INF) e pode ser filtrada no inventário e no stock:

| Área no ficheiro do armazém | Área técnica |
|---|---|
| DEI_TORRE | MBB |
| DEI_FTTH, AIRPON | FBB |
| ENERGIA (todas as variantes) | INF ENR |
| outras DEI (DEI, DEI PROJETO, DEI_RAM…) | DEI (outros) |
| armazém de Projeto sem área no ficheiro | DEI (outros) |

### Código SAP e códigos de fornecedor

O **código SAP** (coluna `Material` do ficheiro do armazém, ex.: 20076) é o **ID oficial** do material. Os fornecedores usam os seus próprios códigos (ex.: Part Number da Huawei `01076667`) e a DEI controla essa correspondência:

- Cada material tem a lista **Códigos de fornecedor** (fornecedor, código do fornecedor, modelo, descrição do fornecedor), na ficha do material.
- **Packing List**: o código da linha é do fornecedor. Se já estiver associado a um código SAP, a entrada vai **diretamente** para esse material. Se não, o material fica **provisório — "sem código SAP"** (alarme *Sem código SAP*).
- **Associar ao código SAP** (ficha do material): se o código SAP já existe no stock, os dois **juntam-se** (stock, movimentos, nºs de série, contagens e códigos passam para o material SAP); se não existe, o material passa a ter esse código SAP. O código do fornecedor fica registado e as próximas entradas com ele vão diretas.
- Compras DEI e entradas manuais usam o código SAP.
- A pesquisa do stock encontra o material pelo código SAP ou por qualquer código de fornecedor.
- O mesmo código SAP genérico (ex.: "Poste") pode ter vários códigos de fornecedor / modelos associados.

### Regras do inventário

- **Importar o ficheiro SAP** (Inventário → "Importar o ficheiro do SAP do armazém") cria **um inventário aberto por armazém**, com a área DEP e a data escolhidas.
- **Fechar** um inventário de um armazém acerta **só o stock desse armazém**: a diferença para o saldo da plataforma fica como movimento de Ajuste. Um inventário novo do mesmo armazém volta a acertar — fica sempre a **contagem mais recente**.
- Opção ao fechar **"Não contados passam a 0"**: o que tinha stock no armazém e não aparece na nova contagem fica a zero.
- Materiais novos são criados no catálogo ao fechar (com a área técnica da linha). O nome do catálogo não é substituído: se o armazém usar outro nome para o mesmo código, fica o alerta **Nome diferente** para decidir (manter o nome do catálogo ou usar o nome recebido).
- Um inventário fechado não pode ser apagado; um aberto pode.
- Entradas (Packing List, compras) e saídas também indicam o armazém; a saída valida o stock do armazém escolhido.

### Base de dados — Stock

```mermaid
erDiagram
    MateriaisStock ||--o{ MovimentosStock : "movimentos"
    MateriaisStock ||--o{ NumerosSerieStock : "nºs de série"
    MateriaisStock ||--o{ DivergenciasNomeMaterial : "outros nomes"
    MateriaisStock ||--o{ CodigosFornecedorMaterial : "códigos de fornecedor"
    EntradasStock ||--o{ MovimentosStock : "entrada"
    InventariosStock ||--o{ LinhasInventario : "contagem"
    InventariosStock |o..o{ MovimentosStock : "ajustes"
    ArmazensStock |o..o{ MovimentosStock : "Armazem = Codigo"
    ArmazensStock |o..o{ InventariosStock : "Armazem = Codigo"
    ProjetosPlano |o..o{ MovimentosStock : "saída para projeto"
    Pedidos |o..o{ EntradasStock : "compra DEI"

    MateriaisStock {
        int Id PK
        string Codigo "código SAP (único)"
        bool CodigoProvisorio "true = só código do fornecedor"
        string Descricao
        string Unidade
        string AreaTecnica "MBB, FBB, INF ENR, DEI (outros)"
        decimal StockMinimo
        decimal PrecoUnitario
    }
    ArmazensStock {
        int Id PK
        string Centro "CPRA, CMIN"
        string Codigo "Depósito: DPRP, DSRP..."
        string Nome "Praia_Projeto..."
    }
    MovimentosStock {
        int Id PK
        int MaterialStockId FK
        string Area "DepInf..."
        string Armazem "Depósito"
        string Tipo "Entrada, Saida, Ajuste"
        decimal Quantidade
        int EntradaStockId FK
        int InventarioStockId FK
        int ProjetoPlanoId FK
    }
    InventariosStock {
        int Id PK
        string Area
        string Armazem "Depósito contado"
        string Centro
        date Data
        string Estado "Aberto, Fechado"
    }
    LinhasInventario {
        int Id PK
        int InventarioStockId FK
        string Codigo
        string Descricao "nome usado pelo armazém"
        string AreaTecnica
        decimal QuantidadeContada
        decimal QuantidadeSistema "saldo ao fechar"
        string Observacoes "Área no ficheiro"
    }
    EntradasStock {
        int Id PK
        string Tipo "PackingList, CompraDei, Manual"
        string Area
        string Armazem
        string Numero
        string Contrato1Mais1
    }
    CodigosFornecedorMaterial {
        int Id PK
        int MaterialStockId FK
        string Fornecedor
        string Codigo "código do fornecedor"
        string Modelo
        string Descricao
    }
    DivergenciasNomeMaterial {
        int Id PK
        int MaterialStockId FK
        string NomeRecebido
        string Origem "Inventario, PackingList, Entrada"
        string Estado "Pendente, MantidoCatalogo, NomeAtualizado"
    }
```
