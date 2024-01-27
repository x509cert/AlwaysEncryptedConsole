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

using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

partial class Program
{
    private const string _EnvVar = "ConnectContosoHR";

    // Helper function to dump binary data
    // Can truncate the output if needed
    public static string ByteArrayToHexString(byte[] byteArray, int maxLen = 16)
    {
        StringBuilder hex = new(byteArray.Length * 2);
        foreach (byte b in byteArray)
            hex.AppendFormat("{0:x2}", b);

        return hex.ToString()[..maxLen];
    }

    // Build SQL Connection String 
    public static string GetSQLConnectionString(bool useAE = true)
    {
        string? sqlConn = 
            Environment.GetEnvironmentVariable(_EnvVar, EnvironmentVariableTarget.Process) 
            ?? throw new ArgumentException($"Missing environment variable, {_EnvVar}");

        // Add AE settings if needed
        if (useAE)
            sqlConn += ";Column Encryption Setting=Enabled;Attestation Protocol=None;";

        return sqlConn;
    }

    // Login to Azure and get token to Azure SQL DB OAuth2 token
    public static (TokenCredential? tok, string? oauth2Sql) LoginToAure()
    {
        try
        {
            var credential = new AzureCliCredential();
            var oauth2TokenSql = credential.GetToken(
                    new TokenRequestContext(
                        ["https://database.windows.net/.default"])).Token;
            return (credential, oauth2TokenSql);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return (null, null);
        }
    }

    // We need to register the use of AKV for AE
    public static void RegisterAkvForAe(TokenCredential cred)
    {
        var akvAeProvider = new SqlColumnEncryptionAzureKeyVaultProvider(cred);
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(
            customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>() {
                { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvAeProvider }
            });
    }
}