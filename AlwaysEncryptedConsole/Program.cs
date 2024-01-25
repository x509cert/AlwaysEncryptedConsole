//********************************************************* 
// 
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
using Azure.Core.Diagnostics;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;

partial class Program
{
    static void Main()
    {
        // flip this to true or false to use or not use AE
        bool useAlwaysEncrypted = true;

        Console.WriteLine($"Starting... using Always Encrypted with Enclaves? {(useAlwaysEncrypted ? "Yes" : "No")}");

        // login to Azure and get token to Azure SQL DB OAuth2 token
        Console.WriteLine("Connecting to Azure");

        // Setup a listener to monitor logged events.
        // This is in case of MSAL errors, you can see what happened
        // Sometimes DefaultAzureCredential fails, learn more at:
        // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/identity/Azure.Identity/TROUBLESHOOTING.md#troubleshoot-defaultazurecredential-authentication-issues
        //using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();

        DefaultAzureCredential credential;
        string oauth2TokenSql;

        try
        {
            credential = new DefaultAzureCredential();
            oauth2TokenSql = credential.GetToken(
                    new TokenRequestContext(
                        ["https://database.windows.net/.default"])).Token;
        } 
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return;
        }

        // login to AKV and then Azure SQL
        Console.WriteLine("Getting SQL connection info from Azure Key Vault");
        var connectionString = GetSQLConnectionString(credential, useAE: useAlwaysEncrypted);

        Console.WriteLine("Connecting to Azure SQL DB");

        // Connect to Azure SQL DB using EntraID AuthN rather than Windows or SQL AuthN
        SqlConnection conn = new(connectionString) {
            AccessToken = oauth2TokenSql
        };
        conn.Open();

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
            // register AKV for AE use by this code
            RegisterAkvForAe(credential);

            // query to find minimum salary with specifical SSN
            string query =
                "SELECT [SSN], [Salary], [LastName], [FirstName] " +
                "FROM Employees WHERE [Salary] > @MinSalary AND [SSN] LIKE @SSN " +
                "ORDER by [Salary] DESC";

            sqlCommand = conn.CreateCommand();
            sqlCommand.CommandText = query;

            // we MUST use parameters
            var minSalaryParam = sqlCommand.CreateParameter();
            minSalaryParam.ParameterName = @"@MinSalary";
            minSalaryParam.DbType = DbType.Currency;
            minSalaryParam.Value = 50_000;
            minSalaryParam.Direction = ParameterDirection.Input;
            sqlCommand.Parameters.Add(minSalaryParam);

            SqlParameter ssnParam = new("@SSN", SqlDbType.Char);
            ssnParam.Value = "6%";
            sqlCommand.Parameters.Add(ssnParam);
        }

        // now read the data
        var stopwatch = Stopwatch.StartNew();

        SqlDataReader data;

        Console.WriteLine("Performing Query");
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

        // get column headers
        Console.WriteLine("Fetching Data");
        for (int i = 0; i < data.FieldCount; i++)
            Console.Write(data.GetName(i) + ", ");

        Console.WriteLine();

        // get data
        while (data.Read())
        {
            for (int i = 0; i < data.FieldCount; i++)
            {
                var value = data.GetValue(i);
                var type = data.GetFieldType(i);

                if (value is not null)
                {
                    // if the data is a byte array (ie; ciphertext) dump the hex string
                    if (type == typeof(byte[]))
#pragma warning disable CS8604 // Possible null reference argument. There *IS* a check two lines up!
                        value = ByteArrayToHexString(value as byte[], 16);
#pragma warning restore CS8604 
                } 
                else
                {
                    value = "?";
                }

                Console.Write(value + ", ");
            }
            Console.WriteLine();
        }
    }
}