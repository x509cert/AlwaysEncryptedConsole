using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System.Text;
using Azure.Security.KeyVault.Secrets;

static string ByteArrayToHexString(byte[] byteArray, int max)
{
    StringBuilder hex = new StringBuilder(byteArray.Length * 2);
    foreach (byte b in byteArray)
        hex.AppendFormat("{0:x2}", b); 

    return hex.ToString()[..max];
}

// Get URI of AKV from env var
static string? GetAKVConnectionString() 
    => Environment.GetEnvironmentVariable("AKVContosoHR", EnvironmentVariableTarget.Process);

// Get SQL Connection String from AKV
static string GetSQLConnectionString(DefaultAzureCredential cred)
{
    string? akvConn = GetAKVConnectionString();
    var client = new SecretClient(new Uri(akvConn), cred);
    var sqlConn = client.GetSecret("ContosoHRSQLString");
    return sqlConn.Value.Value;
}

var credential = new DefaultAzureCredential();
var oauth2TokenSql = credential.GetToken(
        new TokenRequestContext(
            new[] { "https://database.windows.net/.default" })).Token;

var connectionString = GetSQLConnectionString(credential);
SqlConnection conn = new(connectionString) {
    AccessToken = oauth2TokenSql
};

conn.Open();

SqlCommand sqlCommand = new("SELECT * FROM Employees", conn);
using var sqlDataReader = sqlCommand.ExecuteReader();

while (sqlDataReader.Read())
{
    for (int i = 0; i < sqlDataReader.FieldCount; i++)
    {
        var value = sqlDataReader.GetValue(i);
        var type = sqlDataReader.GetFieldType(i);

        if (type == typeof(byte[]))
            value = ByteArrayToHexString(value as byte[], 8);

        Console.Write(value + "\t"); 
    }
    Console.WriteLine(); 
}
