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
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

partial class Program
{
    private const string _EnvVar = "ConnectContosoHR";

    // Helper function to dump binary data
    // can truncate the output if needed
    public static string ByteArrayToHexString(byte[] byteArray, int max)
    {
        StringBuilder hex = new StringBuilder(byteArray.Length * 2);
        foreach (byte b in byteArray)
            hex.AppendFormat("{0:x2}", b);

        return hex.ToString()[..max];
    }

    // Get SQL Connection String from env var
    private static string? GetSQLConnectionStringFromEnv()
        => Environment.GetEnvironmentVariable(_EnvVar, EnvironmentVariableTarget.Process);

    // Build SQL Connection String 
    public static string GetSQLConnectionString(bool useAE = true)
    {
        string? sqlConn = GetSQLConnectionStringFromEnv();
        if (sqlConn is null)
            throw new ArgumentException($"Missing environment variable, {_EnvVar}");

        // add AE settings if needed
        if (useAE)
            sqlConn += ";Column Encryption Setting=Enabled;Attestation Protocol=None;";

        return sqlConn;
    }

    // We need to register the use of AKV for AE
    public static void RegisterAkvForAe(DefaultAzureCredential cred)
    {
        var akvAeProvider = new SqlColumnEncryptionAzureKeyVaultProvider(cred);
        SqlConnection.RegisterColumnEncryptionKeyStoreProviders(
            customProviders: new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>() {
                { SqlColumnEncryptionAzureKeyVaultProvider.ProviderName, akvAeProvider }
            });
    }
}