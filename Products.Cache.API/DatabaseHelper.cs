using Npgsql;

namespace Products.Cache.API;

public class DatabaseHelper: IDatabaseHelper
{
    private static NpgsqlConnection NewConnection()
    {
        return new NpgsqlConnection(
            Settings.ConnStr.ConnectionString
        );
    }

    public NpgsqlConnection? CurrentConnection { get; private set; }

    public async Task<NpgsqlConnection> OpenConnection()
    {
        CurrentConnection ??= NewConnection();
        
        var connected = false;
        var count = 0;

        while (!connected) 
        {
            try {
                await CurrentConnection.OpenAsync();
                connected = true;
            }
            catch (NpgsqlException) {
                await Task.Delay(1_000);
            }
            
            count++;
            
            if (count > 10) {
                throw new Exception("could not connect to postgres");
            }
        }

        return CurrentConnection;
    }

    public async Task CloseConnection()
    {
        try
        {
            await CurrentConnection?.CloseAsync()!;
            await CurrentConnection.DisposeAsync();
            CurrentConnection = null;
        }
        catch (NpgsqlException)
        {
        }
    }
}

public interface IDatabaseHelper
{
    Task<NpgsqlConnection> OpenConnection();
    Task CloseConnection();
    NpgsqlConnection? CurrentConnection { get; }
}