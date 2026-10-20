# Publicar a ComparacaoPropostas na SmarterASP.NET

Ficheiros já preparados neste repositório:

- `publish/` — build de produção pronto a copiar para o servidor (também comprimido em `ComparacaoPropostas_publish.zip`).
- `appsettings.Production.json` — configuração de produção (connection string e SMTP por preencher).
- `publish/web.config` — já configurado para forçar `ASPNETCORE_ENVIRONMENT=Production` no IIS.

## Passos

### 1. Criar a base de dados no painel

No painel da SmarterASP.NET (secção **Hosting**), vai a **MSSQL Databases** e cria uma nova base de dados.
Guarda os dados que eles derem: **servidor**, **nome da base de dados**, **utilizador** e **password**.

### 2. Preencher `appsettings.Production.json`

Abre `appsettings.Production.json` e substitui os placeholders pela connection string real:

```json
"DefaultConnection": "Server=SUBSTITUIR_SERVIDOR_SMARTERASP;Database=SUBSTITUIR_NOME_BD;User Id=SUBSTITUIR_USER;Password=SUBSTITUIR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Se quiseres notificações por email a funcionar em produção, preenche também a secção `SmtpSettings` (host, user, password do teu servidor SMTP).

### 3. Aplicar as migrações à base de dados remota

Com a connection string já preenchida, no teu computador:

```bash
cd "C:\Users\Administrator.CONCORRENCIA\source\repos\ComparacaoPropostas"
dotnet ef database update --connection "<connection string do passo 2>"
```

Isto cria todas as tabelas na base de dados da SmarterASP.NET.

### 4. Gerar o publish (só é preciso repetir se voltares a alterar código)

```bash
dotnet publish ComparacaoPropostas.csproj -c Release -o ./publish
```

Depois de gerares um novo `publish/`, copia o `appsettings.Production.json` atualizado para dentro dessa pasta (ele substitui o placeholder que vem do build) e confirma que o `web.config` mantém o bloco `<environmentVariables>` (o gerado automaticamente pelo `dotnet publish` não o inclui — foi adicionado manualmente desta vez).

### 5. Enviar os ficheiros para o servidor

No painel, secção **Hosting** → **File Manager** (ou FTP, com as credenciais que o painel indica):

- Envia todo o conteúdo da pasta `publish/` (ou descomprime `ComparacaoPropostas_publish.zip`) para a pasta raiz do site (normalmente `wwwroot` ou similar, indicado no painel).

Alternativa mais simples: no Visual Studio, botão direito no projeto → **Publish** → escolhe **Web Deploy** e cola o perfil de publicação que a SmarterASP.NET disponibiliza no painel (em **Hosting** → algo como "Web Deploy" ou "Publish Profile") — isto trata do envio automaticamente.

### 6. Confirmar a versão .NET no painel

Em **Hosting** → **ASP.NET Settings** (ou equivalente), confirma que a **.NET Core Version** está definida para **.NET 8**.

### 7. Associar o domínio/subdomínio

Em **Hosting** → **Domain**, associa o domínio ou subdomínio pretendido a este site.

### 8. Testar

Abre o URL do site e confirma que a página inicial carrega. Se der erro 500, ativa temporariamente `stdoutLogEnabled="true"` no `web.config` para veres o erro exato nos logs (pasta `logs/` do site).

---

# Integração com o Airtable (App → Airtable)

Cada gravação na app (processos, critérios, propostas, itens das propostas, avaliações, catálogo de itens) é enviada automaticamente para a base Airtable **appiAu2t9S3VoA1bO**, em segundo plano, poucos segundos depois. A app é a fonte de verdade: o que for alterado à mão no Airtable em registos sincronizados é substituído na próxima gravação desse processo na app.

## Como funciona

- `Services/Airtable/AirtableSyncInterceptor.cs` — deteta as alterações em cada `SaveChanges` e regista-as na tabela `AirtableSyncPendentes` (na mesma transação da gravação). Não foi preciso alterar nenhum controller.
- `Services/Airtable/AirtableSyncWorker.cs` — processa a fila em segundo plano; erros ficam registados e são repetidos (1, 2, 4… minutos, até 1 hora).
- `Services/Airtable/AirtableSyncService.cs` — envia o processo completo (upsert pelo campo **ID App** de cada tabela do Airtable) e apaga no Airtable o que deixou de existir na app.
- `Services/Airtable/AirtableMapper.cs` — correspondência campo a campo. Valores sempre em CVE; Peso 0–100 → percentagem; Estados: Criado→Aberto, Recebido→Em Avaliação, Concluído→Decidido, Cancelado→Cancelado / Recebida, Em Análise→Em Avaliação, Aceite→Aprovada, Rejeitada→Reprovada.
- Avaliações: o Airtable recebe **uma avaliação por Proposta × Critério com a nota final da app** (média dos avaliadores ou nota automática), para que a *Pontuação Ponderada* do Airtable seja igual à pontuação da app.
- Página de estado na app: menu **Integrações → Airtable** (fila, erros, botões *Ressincronizar tudo* e *Tentar erros agora*).

## Ativar

1. Em https://airtable.com/create/tokens cria um *Personal Access Token*:
   - Scopes: `data.records:read` e `data.records:write`
   - Access: apenas a base **Comparação de Propostas**
2. No `appsettings.Production.json` do servidor (não vai para o git) acrescenta:

   ```json
   "Airtable": {
     "Enabled": true,
     "Token": "patXXXXXXXXXXXXXX.XXXXXXXX"
   }
   ```

   (alternativa: variáveis de ambiente `Airtable__Enabled=true` e `Airtable__Token=...` no bloco `<environmentVariables>` do `web.config`).
3. Publica a nova versão (passos 4 e 5 acima). A migração `AddAirtableSync` cria as tabelas `AirtableLinks` e `AirtableSyncPendentes`; a app aplica-a sozinha no arranque (`Database.Migrate()`), ou corre `dotnet ef database update` como no passo 3.
4. Abre a app → **Integrações → Airtable** → **Ressincronizar tudo** para a carga inicial dos processos existentes.

## Cuidados

- **Não apagar nem renomear-para-outra-coisa os campos "ID App"** no Airtable — são a chave que evita duplicados. (Renomear outros campos ou tabelas não faz mal: a integração usa os IDs internos.) Se apagares um campo usado pela integração, os envios passam a falhar e o erro aparece na página de estado; os IDs estão em `Services/Airtable/AirtableSchema.cs`.
- Registos criados à mão no Airtable (sem ID App) nunca são tocados pela sincronização.
- Quando um processo passa a **Concluído** na app, fica **Decidido** no Airtable e dispara a automação "Classificação de propostas ao decidir processo". Para processos vindos da app, os campos *Criado por* e *Equipa a Notificar* ficam vazios, por isso essa automação não tem a quem enviar (a app já envia o seu próprio e-mail de resultado). Se não quiseres essas execuções falhadas, acrescenta à automação a condição "ID App está vazio".
- O SmarterASP (IIS) pode desligar a aplicação quando não tem visitas; o envio retoma no pedido seguinte — nada se perde, porque a fila está na base de dados.
