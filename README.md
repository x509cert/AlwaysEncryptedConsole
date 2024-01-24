This is a sample C# client app used to demo the coding aspects of Always Encrypted with VBS enclaves.

To setup, you need an environment variable named AKVContosoHR that points to your Azure Key Vault. eg; `AKVContosoHR=https://XXXXdemovault.vault.azure.net/`.

The AKV has a secret named ContosoHRSQLString that is the connection string to your Azure SQL Instance. eg; `Server=tcp:XXXXenclavedemoserver.database.windows.net;Database=ContosoHR;`

When the code runs, there's a flag in the code:
`bool useAlwaysEncrypted = true;`

You can set this to false and run the code, and then true and re-run.

- When AE==false, you will see a hex dump of the ciphertext fields, SSN and Salary. 
- When AE==true, the code will change the connection string to support AE and then display the plaintext for SSN and Salary.
