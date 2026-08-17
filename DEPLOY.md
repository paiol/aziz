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
