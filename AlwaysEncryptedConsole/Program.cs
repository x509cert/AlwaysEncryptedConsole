using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System.Data;

partial class Program
{
    static void Main()
    {
        // login to Azure
        var credential = new DefaultAzureCredential();
        var oauth2TokenSql = credential.GetToken(
                new TokenRequestContext(
                    new[] { "https://database.windows.net/.default" })).Token;

        // login to Azure SQL
        var connectionString = GetSQLConnectionString(credential, useAE: true);
        SqlConnection conn = new(connectionString) {
            AccessToken = oauth2TokenSql
        };

        conn.Open();

        // register AKV for AE use by this code
        RegisterAkvForAe(credential);

        // Perform a query
        string query =
            "SELECT Top 10 SSN, Salary, LastName, FirstName FROM Employees WHERE Salary > @MinSalary or SSN LIKE @SSN";
        
        SqlCommand sqlCommand = new(query, conn);

        // set the minimum salary
        // we MUST use parameters
        SqlParameter minSalaryParam = new("@MinSalary", SqlDbType.Money);
        minSalaryParam.Value = 50000;
        sqlCommand.Parameters.Add(minSalaryParam);

        // now read the data
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