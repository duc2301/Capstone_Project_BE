using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connString = "Host=localhost; Port=5432; Database=CapstoneProjectDb; Username=postgres; Password=12345;";
        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        using (var cmd = new NpgsqlCommand("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20260723151828_AddJointVenture', '10.0.10') ON CONFLICT DO NOTHING;", conn))
        {
            int rows = cmd.ExecuteNonQuery();
            Console.WriteLine("Inserted " + rows + " row(s) into __EFMigrationsHistory.");
        }
    }
}
