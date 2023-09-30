namespace Products.Cache.API;

public static class Settings
{
    public static Npgsql.NpgsqlConnectionStringBuilder ConnStr { get; set; }
    public static bool Env { get; set; }
}