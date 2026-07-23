using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=localhost; Port=5432; Database=CapstoneProjectDb; Username=postgres; Password=12345;";
        try
        {
            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();
                
                string[] tables = { "FileNotes", "MarkupSets", "FileVersionLoiChecks" };
                foreach(var table in tables) 
                {
                    using (var cmd = new NpgsqlCommand($"DELETE FROM \"{table}\" WHERE \"FileVersionId\" NOT IN (SELECT \"Id\" FROM \"FileVersionStates\");", conn))
                    {
                        try {
                            int rows = cmd.ExecuteNonQuery();
                            Console.WriteLine($"Deleted {rows} dirty rows from {table}.");
                        } catch(Exception e) {
                            Console.WriteLine($"Skipped {table}: {e.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}

