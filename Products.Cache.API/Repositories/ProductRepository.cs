using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace Products.Cache.API.Repositories;

public class ProductRepository
{
    readonly IDatabaseHelper _databaseHelper;
    public ProductRepository(IDatabaseHelper databaseHelper)
    {
        _databaseHelper = databaseHelper;
    }
    
    public async Task<Product[]> ExistsSkuList(string[] sku, CancellationToken cancellationToken)
    {
        var product = new List<Product>();
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        var sql = @"SELECT A.ID, A.SKU, A.TITLE, A.DESCRIPTION, A.REDUCEDTITLE, A.PRICE, A.SEARCHTEXT 
                      FROM PUBLIC.PRODUCTS A 
                     WHERE #WHERE#";

        var where = string.Join(" OR ", sku.Select((_, i) => $" A.SKU = @SKU{i}"));
        
        sql = sql.Replace("#WHERE#", where);  
        
        // Foreach sku and create a where clause
        for (var i = 0; i < sku.Length; i++)
        {
            var skuItem = sku[i];
            
            command.Parameters.Add(new NpgsqlParameter($"SKU{i}", NpgsqlDbType.Varchar){ Value = skuItem });
        }
        command.CommandText = sql;
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            product.Add(new Product()
            {
                ID = reader.GetGuid(0),
                SKU = reader.GetString(1),
                TITLE = reader.GetString(2),
                DESCRIPTION = reader.GetString(3),
                REDUCEDTITLE = reader.GetString(4),
                PRICE = reader.GetDouble(5),
                SEARCHTEXT = reader.GetString(6)
            });
        }

        await _databaseHelper.CloseConnection();
        return product.ToArray();
    }
    
    // get product by sku
    public async Task<Product?> GetBySku(string sku, CancellationToken cancellationToken)
    {
        Product? product = null;
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"SELECT A.ID, A.SKU, A.TITLE, A.DESCRIPTION, A.REDUCEDTITLE, A.PRICE, A.SEARCHTEXT 
                                  FROM PUBLIC.PRODUCTS A 
                                 WHERE A.SKU = @SKU";
        
        command.Parameters.Add(new NpgsqlParameter("SKU", NpgsqlDbType.Varchar){ Value = sku });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            product = new Product()
            {
                ID = reader.GetGuid(0),
                SKU = reader.GetString(1),
                TITLE = reader.GetString(2),
                DESCRIPTION = reader.GetString(3),
                REDUCEDTITLE = reader.GetString(4),
                PRICE = reader.GetDouble(5),
                SEARCHTEXT = reader.GetString(6)
            };
        }

        await _databaseHelper.CloseConnection();
        return product;
    }

    // get products by page
    public async Task<Product[]?> GetByPage(int page, int pageSize, CancellationToken cancellationToken)
    {
        Product[]? products = null;
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"SELECT A.ID, A.SKU, A.TITLE, A.DESCRIPTION, A.REDUCEDTITLE, A.PRICE, A.SEARCHTEXT 
                                  FROM PUBLIC.PRODUCTS A 
                                 ORDER BY A.ID DESC 
                                 LIMIT @PAGESIZE 
                                OFFSET @PAGE";
        command.Parameters.Add(new NpgsqlParameter("pageSize", pageSize));
        command.Parameters.Add(new NpgsqlParameter("page", (page - 1) * pageSize));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (reader.HasRows)
        {
            products = new Product[pageSize];
            var i = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                products[i] = new Product()
                {
                    ID = reader.GetGuid(0),
                    SKU = reader.GetString(1),
                    TITLE = reader.GetString(2),
                    DESCRIPTION = reader.GetString(3),
                    REDUCEDTITLE = reader.GetString(4),
                    PRICE = reader.GetDouble(5),
                    SEARCHTEXT = reader.GetString(6)
                };
                i++;
            }
            await reader.CloseAsync();
        }

        await _databaseHelper.CloseConnection();
        return products?.Where(r => r != null).ToArray();
    }

    public async Task<Product?> GetById(string id, CancellationToken cancellationToken)
    {
        Product? product = null;
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"SELECT a.id, a.sku, a.title, a.description, a.reducedtitle, a.price, a.searchtext 
                                  FROM public.products a where a.id = @id";
        
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid){ Value = Guid.Parse(id) });
        
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (reader.HasRows)
        {
            product = new Product()
            {
                ID = reader.GetGuid(0),
                SKU = reader.GetString(1),
                TITLE = reader.GetString(2),
                DESCRIPTION = reader.GetString(3),
                REDUCEDTITLE = reader.GetString(4),
                PRICE = reader.GetDouble(5),
                SEARCHTEXT = reader.GetString(6)
            };
        }

        await reader.CloseAsync();
        await _databaseHelper.CloseConnection();
        return product;
    }
    
    // insert product
    public async Task Insert(Product product, CancellationToken cancellationToken)
    {
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"INSERT INTO public.products (id, sku, title, description, reducedtitle, price) 
                                  VALUES (@id, @sku, @title, @description, @reducedTitle, @price)";
        
        command.Parameters.Add(new NpgsqlParameter("id", product.ID));
        command.Parameters.Add(new NpgsqlParameter("sku", product.SKU));
        command.Parameters.Add(new NpgsqlParameter("title", product.TITLE));
        command.Parameters.Add(new NpgsqlParameter("description", product.DESCRIPTION));
        command.Parameters.Add(new NpgsqlParameter("reducedTitle", product.REDUCEDTITLE));
        command.Parameters.Add(new NpgsqlParameter("price", product.PRICE));
        
        await command.ExecuteNonQueryAsync(cancellationToken);
        await _databaseHelper.CloseConnection();
    }
    
    public async Task Insert(IEnumerable<Product> products, CancellationToken cancellationToken)
    {
        await using var connection = await _databaseHelper.OpenConnection();
        await using var batch = connection.CreateBatch();
        const string sql = """
                           INSERT INTO public.products (id, sku, title, description, reducedTitle, price)
                                                             VALUES (@id, @sku, @title, @description, @reducedTitle, @price)
                           """;

        foreach (var product in products)
        {
            var command = new NpgsqlBatchCommand(sql);
            command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid){ Value =  product.ID });
            command.Parameters.Add(new NpgsqlParameter("sku", NpgsqlDbType.Varchar) { Value = product.SKU });
            command.Parameters.Add(new NpgsqlParameter("title", NpgsqlDbType.Varchar) { Value = product.TITLE });
            command.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Varchar) { Value = product.DESCRIPTION });
            command.Parameters.Add(new NpgsqlParameter("reducedTitle", NpgsqlDbType.Varchar) { Value = product.REDUCEDTITLE });
            command.Parameters.Add(new NpgsqlParameter("price", NpgsqlDbType.Double) { Value = product.PRICE });
            
            batch.BatchCommands.Add(command);
        }
        await batch.ExecuteNonQueryAsync(cancellationToken);
        
        await _databaseHelper.CloseConnection();
    }
    
    // update product by id
    public async Task Update(Product product, CancellationToken cancellationToken)
    {
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"UPDATE public.products 
                                   SET sku = @sku, 
                                       title = @title,
                                       description = @description,
                                       reducedtitle = @reducedTitle,
                                       price = @price
                                 WHERE id = @id";
        
        command.Parameters.Add(new NpgsqlParameter("id", product.ID));
        command.Parameters.Add(new NpgsqlParameter("sku", product.SKU));
        command.Parameters.Add(new NpgsqlParameter("title", product.TITLE));
        command.Parameters.Add(new NpgsqlParameter("description", product.DESCRIPTION));
        command.Parameters.Add(new NpgsqlParameter("reducedTitle", product.REDUCEDTITLE));
        command.Parameters.Add(new NpgsqlParameter("price", product.PRICE));
        
        await command.ExecuteNonQueryAsync(cancellationToken);
        await _databaseHelper.CloseConnection();
    }
    
    // delete product by id
    public async Task Delete(string id, CancellationToken cancellationToken)
    {
        await using var connection = await _databaseHelper.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandType = CommandType.Text;
        command.CommandText = @"DELETE FROM public.products WHERE id = @id";
        
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid){ Value =  id });
        
        await command.ExecuteNonQueryAsync(cancellationToken);
        await _databaseHelper.CloseConnection();
    }
}