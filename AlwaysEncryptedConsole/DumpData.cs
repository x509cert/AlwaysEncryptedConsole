using Microsoft.Data.SqlClient;

partial class Program
{
    public static void DumpData(SqlDataReader data)
    {
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

                if (value is not null)
                {
                    var type = data.GetFieldType(i);

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

