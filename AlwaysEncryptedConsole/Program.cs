//********************************************************* 
// Copyright (c) Microsoft. All rights reserved. 
// This code is licensed under the MIT License (MIT). 
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF 
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY 
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR 
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT. 
// 
// Author: Michael Howard, Azure Data Security
//*********************************************************

using Azure.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;

partial class Program
{
    static void Main()
    {
        // flip this to true or false to use or not use AE
        bool useAlwaysEncrypted = true;

        Console.WriteLine($"Cold Start\nUse Always Encrypted with Enclaves? {(useAlwaysEncrypted ? "Yes" : "No")}");

        // login to Azure and get Azure SQL DB OAuth2 token
        Console.WriteLine("Connecting to Azure");
        (TokenCredential? credential, string? oauth2TokenSql) = LoginToAure();
        if (credential is null || oauth2TokenSql is null)
            throw new ArgumentNullException("Unable to login to Azure");

        Console.WriteLine("Connecting to Azure SQL DB");

        // Connect to Azure SQL DB using EntraID AuthN rather than Windows or SQL AuthN
        var connectionString = GetSQLConnectionString(useAlwaysEncrypted);
        using SqlConnection conn = new(connectionString) {
            AccessToken = oauth2TokenSql
        };
        conn.Open();

        // Register the enclave attestation URL
        if (useAlwaysEncrypted)
            RegisterAkvForAe(credential);

        for (int j = 0; j < 2; j++)
        {
            // from here on is real database work
            SqlCommand sqlCommand;

            Console.WriteLine("Building Query");
            if (useAlwaysEncrypted == false)
            {
                string query =
                    "SELECT Top 10 SSN, Salary, LastName, FirstName " +
                    "FROM Employees";

                sqlCommand = new(query, conn);
            }
            else
            {
                // query to find minimum salary with specifical SSN
                string query =
                    "SELECT [SSN], [Salary], [LastName], [FirstName] " +
                    "FROM Employees WHERE [Salary] > @MinSalary AND [SSN] LIKE @SSN " +
                    "ORDER by [Salary] DESC";

                sqlCommand = conn.CreateCommand();
                sqlCommand.CommandText = query;

                // we MUST use parameters
                SqlParameter minSalaryParam = new("@MinSalary", SqlDbType.Money) {
                    Value = 50_000
                };
                sqlCommand.Parameters.Add(minSalaryParam);

                SqlParameter ssnParam = new("@SSN", SqlDbType.Char) {
                    Value = "6%"
                };
                sqlCommand.Parameters.Add(ssnParam);
            }

            var stopwatch = Stopwatch.StartNew();

            Console.WriteLine("Performing Query");
            SqlDataReader data;
            try
            {
                data = sqlCommand.ExecuteReader();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }

            stopwatch.Stop();
            Console.WriteLine($"Query took {stopwatch.ElapsedMilliseconds}ms");

            DumpData(data);
            data.Close();
        }
    }
}