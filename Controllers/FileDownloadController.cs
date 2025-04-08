using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

namespace FileTransferApp.Controllers
{
    [Route("api/files/download")]
    [ApiController]
    public class FileDownloadController : Controller
    {
        private readonly string _connectionString;

        public FileDownloadController(IConfiguration configuration)
        {
            _connectionString = "Server=DESKTOP-IB2ECCK\\SQLEXPRESS;Database=FileStorageDB;Trusted_Connection=True;";
        }

        [HttpGet("check-file/{fileName}")]
        public IActionResult CheckFileExists(string fileName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = "SELECT COUNT(*) FROM FileStorage WHERE FileName = @FileName";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    int count = (int)cmd.ExecuteScalar();

                    if (count == 0)
                        return NotFound(new { message = $"File '{fileName}' không tồn tại!" });

                    return Ok(new { message = "File tồn tại." });
                }

                conn.Close();
            }
        }

        [HttpGet("download-file/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            int fileId;
            string fileType;
            byte[] fileData;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = "SELECT FileID, FileData, FileType FROM FileStorage WHERE FileName = @FileName";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FileName", fileName);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return NotFound(new { message = "File '" + fileName + "' không tồn tại!" });

                        fileId = reader.GetInt32(0);
                        fileData = (byte[])reader["FileData"];
                        fileType = reader.GetString(2);
                    }
                }

                string deleteQuery = "DELETE FROM FileStorage WHERE FileID = @FileID";
                using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, conn))
                {
                    deleteCmd.Parameters.AddWithValue("@FileID", fileId);
                    deleteCmd.ExecuteNonQuery();
                }

                string insertDeletedIdQuery = "INSERT INTO DeletedFileIDs (FileID) VALUES (@FileID)";
                using (SqlCommand insertCmd = new SqlCommand(insertDeletedIdQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@FileID", fileId);
                    insertCmd.ExecuteNonQuery();
                }

                conn.Close();
            }

            return File(fileData, fileType, fileName);
        }
    }
}
