using Microsoft.Data.SqlClient;

namespace VSRO_CONTROL_API.VSRO.Tools
{
    public static class Connection
    {
        public static Task<bool> IsConnectionAvailableAsync(string connectionString)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    conn.Close();
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {

                return Task.FromResult(false);
            }
        }
    }
}
