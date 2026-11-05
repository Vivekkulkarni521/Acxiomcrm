# Authentication and access setup

Apply the EF Core migration before starting the application:

```powershell
dotnet ef database update
```

On first startup, the application creates the `Admin`, `Manager`, and `SalesExecutive` roles. To bootstrap the first administrator, provide secrets through environment variables or another secure configuration provider before starting the app:

```powershell
$env:BootstrapAdmin__Email = 'admin@example.com'
$env:BootstrapAdmin__Password = '<a unique strong secret>'
$env:BootstrapAdmin__Name = 'CRM Administrator'
```

The password must satisfy the Identity policy: at least 12 characters with uppercase, lowercase, a number, and a symbol. The app creates the configured account if it does not exist and grants it the Admin role. It never resets an existing account's password. Do not commit credentials to source control or appsettings files. Keep the configured bootstrap account active and protect its secret.

Public registration creates an active `SalesExecutive` account. An Admin can create teams, assign users to teams, change a user's role, activate or deactivate accounts, and assign customers and leads to active CRM users. Managers can view records assigned to themselves and users assigned to the same team. Sales Executives can view only records assigned to themselves. Follow-ups and opportunities inherit access from their linked lead. Existing CRM rows are preserved with no owner; Admin can assign them through the CRM edit forms.
