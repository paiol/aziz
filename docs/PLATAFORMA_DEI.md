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
