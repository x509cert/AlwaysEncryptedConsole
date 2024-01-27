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
        using SqlConnection conn = new(connectionString)
        {
            AccessToken = oauth2TokenSql
        };
        conn.Open();

        // Register the enclave attestation URL
        if (useAlwaysEncrypted)
            RegisterAkvForAe(credential);

        // from here on is real database work
        SqlCommand sqlCommand;

        if (useAlwaysEncrypted == false)
        {
            string query =
                "SELECT Top 10 SSN, Salary, LastName, FirstName " +
                "FROM Employees";

            sqlCommand = new(query, conn);
            DoQuery(sqlCommand);
        }
        else
        {
            ///////////////////////////////////////////////////
            // QUERY #1: Find minimum salary with specific SSN
            string query1 =
                "SELECT [SSN], [Salary], [LastName], [FirstName] " +
                "FROM Employees WHERE [Salary] > @MinSalary AND [SSN] LIKE @SSN " +
                "ORDER by [Salary] DESC";

            sqlCommand = new(query1, conn);

            // MUST use parameters
            SqlParameter minSalaryParam = new("@MinSalary", SqlDbType.Money)
            {
                Value = 50_000
            };
            sqlCommand.Parameters.Add(minSalaryParam);

            SqlParameter ssnParam = new("@SSN", SqlDbType.Char)
            {
                Value = "6%"
            };
            sqlCommand.Parameters.Add(ssnParam);

            DoQuery(sqlCommand);

            ///////////////////////////////////////////////////
            // QUERY #2: sproc to find salary range
            string query2 = "EXEC usp_GetSalary @MinSalary = @MinSalaryRange, @MaxSalary = @MaxSalaryRange";

            sqlCommand = new(query2, conn);

            SqlParameter minSalaryRange = new("@MinSalaryRange", SqlDbType.Money) {
                Value = 40_000
            };
            sqlCommand.Parameters.Add(minSalaryRange);

            SqlParameter maxSalaryRange = new("@MaxSalaryRange", SqlDbType.Money) {
                Value = 42_000
            };
            sqlCommand.Parameters.Add(maxSalaryRange);

            DoQuery(sqlCommand);
        }
    }
    
    static void DoQuery(SqlCommand sqlCommand)
    {
        var stopwatch = Stopwatch.StartNew();

        Console.WriteLine("\nPerforming Query");
        SqlDataReader data;
        try
        {
            data = sqlCommand.ExecuteReader();
        }
        catch (SqlException ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        stopwatch.Stop();
        Console.WriteLine($"Query took [{stopwatch.ElapsedMilliseconds}ms]");

        DumpData(data);
        data.Close();
    }
}