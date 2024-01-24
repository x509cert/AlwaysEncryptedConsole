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

using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider;

partial class Program
{
    // Helper function to dump binary data
    // can truncate the output if needed
    public static string ByteArrayToHexString(byte[] byteArray, int max)
    {
        StringBuilder hex = new StringBuilder(byteArray.Length * 2);
        foreach (byte b in byteArray)
            hex.AppendFormat("{0:x2}", b);

        return hex.ToString()[..max];
    }

    // Get URI of AKV from env var
    public static string? GetAKVConnectionString()
        => Environment.GetEnvironmentVariable("AKVContosoHR", EnvironmentVariableTarget.Process);

    // Get SQL Connection String from AKV
    public static string GetSQLConnectionString(DefaultAzureCredential cred, bool useAE = true)
    {
        string? akvConn = GetAKVConnectionString();
        var client = new SecretClient(new Uri(akvConn), cred);
        var sqlConn = client.GetSecret("ContosoHRSQLString");
        var sql = sqlConn.Value.Value;

        if (useAE)
            sql = sql + "Column Encryption Setting=Enabled;Attestation Protocol=None;";

        return sql;
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