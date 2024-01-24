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
using Azure.Identity;
using Microsoft.Data.SqlClient;
using System.Data;

partial class Program
{
    static void Main()
    {
        bool useAlwaysEncrypted = true;

        Console.WriteLine($"Starting... using Always Encrypted with Enclaves? {(useAlwaysEncrypted ? "Yes" : "No")}");

        // login to Azure
        Console.WriteLine("Connecting to Azure");
        var credential = new DefaultAzureCredential();
        var oauth2TokenSql = credential.GetToken(
                new TokenRequestContext(
                    ["https://database.windows.net/.default"])).Token;

        // login to AKV and then Azure SQL
        Console.WriteLine("Connecting to Azure Key Vault");
        var connectionString = GetSQLConnectionString(credential, useAE: useAlwaysEncrypted);

        Console.WriteLine("Connecting to Azure SQL DB");
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
                "SELECT Top 10 SSN, Salary, LastName, FirstName " +
                "FROM Employees WHERE Salary > @MinSalary AND SSN LIKE @SSN " +
                "ORDER by Salary DESC";

            sqlCommand = new(query, conn);

            // we MUST use parameters
            SqlParameter minSalaryParam = new("@MinSalary", SqlDbType.Money);
            minSalaryParam.Value = 50000;
            sqlCommand.Parameters.Add(minSalaryParam);

            SqlParameter ssnParam = new("@SSN", SqlDbType.Char);
            ssnParam.Value = "6%";
            sqlCommand.Parameters.Add(ssnParam);
        }

        // now read the data
        Console.WriteLine("Performing Query");
        using var sqlDataReader = sqlCommand.ExecuteReader();

        Console.WriteLine("Fetching Data");
        while (sqlDataReader.Read())
        {
            for (int i = 0; i < sqlDataReader.FieldCount; i++)
            {
                var value = sqlDataReader.GetValue(i);
                var type = sqlDataReader.GetFieldType(i);

                // if the data is a byte array (ie; ciphertext) dump the hex string
                if (type == typeof(byte[]))
                    value = ByteArrayToHexString(value as byte[], 16);

                Console.Write(value + "\t");
            }
            Console.WriteLine();
        }
    }
}