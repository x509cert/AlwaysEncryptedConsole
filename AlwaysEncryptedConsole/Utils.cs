using Azure.Identity;
using System.Text;
using Azure.Security.KeyVault.Secrets;

partial class Program
{
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
            sql = sql + "Column Encryption Setting=Enabled";

        return sql;
    }
}