using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

partial class Program
{
    static void Main()
    {
        var credential = new DefaultAzureCredential();
        var oauth2TokenSql = credential.GetToken(
                new TokenRequestContext(
                    new[] { "https://database.windows.net/.default" })).Token;

        var connectionString = GetSQLConnectionString(credential, useAE: true);
        SqlConnection conn = new(connectionString)
        {
            AccessToken = oauth2TokenSql
        };

        conn.Open();

        RegisterAkvForAe(credential);

        SqlCommand sqlCommand = new("SELECT Top 10 SSN, Salary, LastName, FirstName FROM Employees", conn);
        using var sqlDataReader = sqlCommand.ExecuteReader();

        while (sqlDataReader.Read())
        {
            for (int i = 0; i < sqlDataReader.FieldCount; i++)
            {
                var value = sqlDataReader.GetValue(i);
                var type = sqlDataReader.GetFieldType(i);

                if (type == typeof(byte[]))
                    value = ByteArrayToHexString(value as byte[], 16);

                Console.Write(value + "\t");
            }
            Console.WriteLine();
        }
    }
}