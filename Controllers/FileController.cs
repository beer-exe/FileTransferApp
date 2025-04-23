using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace FileTransferApp.Controllers
{
    [Route("api/files")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly string _connectionString;

        public FileController(IConfiguration configuration)
        {
            _connectionString = "Server=DESKTOP-IB2ECCK\\SQLEXPRESS;Database=FileStorageDB;Trusted_Connection=True;";
        }

        [HttpGet]
        public IActionResult GetFiles()
        {
            List<object> fileList = new List<object>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();

                    string query = "SELECT FileID, FileName, CreatedAt FROM FileStorage ORDER BY CreatedAt DESC";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            fileList.Add(new
                            {
                                FileID = reader.GetInt32(0),
                                FileName = reader.GetString(1),
                                CreatedAt = reader.GetDateTime(2)
                            });
                        }
                    }

                    return Ok(fileList);
                }
                catch (SqlException ex)
                {
                    return StatusCode(500, new { message = "Server error, please try again later." });
                }
            }
        }
    }
}
