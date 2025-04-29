using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FileTransferApp.Controllers
{
    [Route("api/files/download")]
    [ApiController]

    public class FileDownloadController : Controller
    {
        private static readonly string _connectionString = "Server=DESKTOP-IB2ECCK\\SQLEXPRESS;Database=FileStorageDB;Trusted_Connection=True;Encrypt=false;TrustServerCertificate=true;";

        public FileDownloadController(IConfiguration configuration) { }

        [HttpGet("check-file/{fileName}")]
        public IActionResult CheckFileExists(string fileName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                try
                {
                    conn.Open();

                    string query = "SELECT COUNT(*) FROM FileStorage WHERE FileName = @FileName";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@FileName", fileName);
                        int count = (int)cmd.ExecuteScalar();

                        if (count == 0)
                            return NotFound(new { message = $"File '{fileName}' not exists!" });

                        return Ok(new { message = "File exists." });
                    }

                    conn.Close();

                }
                catch (SqlException ex)
                {
                    return StatusCode(500, new { message = "Server error, please try again later." });
                }
            }
        }

        [HttpGet("download-file/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            try
            {
                int fileId;
                string fileType;
                byte[] fileData;

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string selectQuery = "SELECT FileID, FileData, FileType FROM FileStorage WHERE FileName = @FileName";
                    using (SqlCommand cmd = new SqlCommand(selectQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@FileName", fileName);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return NotFound(new { message = "File '" + fileName + "' not exists!" });

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

                return File(new MemoryStream(fileData), fileType, fileName);

            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Server error, please try again later." });
            }
        }
    }
}
