using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

var credential = new DefaultAzureCredential();
var oauth2TokenSql = credential.GetToken(
        new TokenRequestContext(
            new[] { "https://database.windows.net/.default" })).Token;

var connectionString = "Server=tcp:vbsenclavedemoserver.database.windows.net;Database=ContosoHR;";
SqlConnection conn = new(connectionString);
conn.AccessToken = oauth2TokenSql;
conn.Open();

SqlCommand sqlCommand = new("SELECT * FROM Employees", conn);
using var sqlDataReader = await sqlCommand.ExecuteReaderAsync();

while (sqlDataReader.Read())
{
    for (int i = 0; i < sqlDataReader.FieldCount; i++)
    {
        string? value = sqlDataReader.GetValue(i).ToString();
        Console.Write(value + "\t"); 
    }
    Console.WriteLine(); 
}
