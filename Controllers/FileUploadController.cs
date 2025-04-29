using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace FileTransferApp.Controllers
{
    [Route("api/files/upload")]
    [ApiController]

    public class FileUploadController : ControllerBase
    {
        private static readonly string _connectionString = "Server=DESKTOP-IB2ECCK\\SQLEXPRESS;Database=FileStorageDB;Trusted_Connection=True;Encrypt=false;TrustServerCertificate=true;";

        private static Dictionary<string, List<byte[]>> _inMemoryChunks = new Dictionary<string, List<byte[]>>();

        public FileUploadController(IConfiguration configuration) { }

        [HttpPost("upload-chunk")]
        public async Task<IActionResult> UploadChunk([FromForm] IFormFile chunk, [FromForm] string fileName, [FromForm] int chunkIndex, [FromForm] int totalChunks)
        {
            try
            {
                if (chunk == null || chunk.Length == 0)
                    return BadRequest("Invalid chunk.");

                using MemoryStream memoryStream = new MemoryStream();
                await chunk.CopyToAsync(memoryStream);
                byte[] chunkBytes = memoryStream.ToArray();

                lock (_inMemoryChunks)
                {
                    if (!_inMemoryChunks.ContainsKey(fileName))
                    {
                        _inMemoryChunks[fileName] = new List<byte[]>(new byte[totalChunks][]);
                    }
                    _inMemoryChunks[fileName][chunkIndex] = chunkBytes;
                }

                return Ok(new { message = $"Chunk {chunkIndex}/{totalChunks} uploaded successfully." });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Server error, please try again later." });
            }
        }

        [HttpGet("check-chunk")]
        public IActionResult CheckChunk([FromQuery] string fileName, [FromQuery] int chunkIndex)
        {
            try
            {
                lock (_inMemoryChunks)
                {
                    if (_inMemoryChunks.ContainsKey(fileName) && _inMemoryChunks[fileName].Count > chunkIndex && _inMemoryChunks[fileName][chunkIndex] != null)
                    {
                        return Ok(new { exists = true });
                    }
                }

                return Ok(new { exists = false });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Server error, please try again later." });
            }
        }

        [HttpPost("merge-chunks")]
        public async Task<IActionResult> MergeChunks([FromForm] string fileName, [FromForm] int totalChunks)
        {
            try
            {
                if (!_inMemoryChunks.ContainsKey(fileName))
                    return BadRequest("Temporary data not found in memory.");

                List<byte[]> chunks = _inMemoryChunks[fileName];
                if (chunks.Count != totalChunks || chunks.Any(c => c == null))
                    return BadRequest("Missing chunk or incomplete data.");

                using MemoryStream finalMemoryStream = new MemoryStream();
                foreach (byte[] chunk in chunks)
                {
                    await finalMemoryStream.WriteAsync(chunk, 0, chunk.Length);
                }

                byte[] fileData = finalMemoryStream.ToArray();
                int newFileId;

                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string checkDeletedIdsQuery = "SELECT TOP 1 FileID FROM DeletedFileIDs ORDER BY FileID ASC";
                    using (SqlCommand checkCmd = new SqlCommand(checkDeletedIdsQuery, conn))
                    {
                        object result = checkCmd.ExecuteScalar();
                        if (result != null)
                        {
                            newFileId = (int)result;
                            string deleteFromDeletedIdsQuery = "DELETE FROM DeletedFileIDs WHERE FileID = @FileID";
                            using (SqlCommand deleteCmd = new SqlCommand(deleteFromDeletedIdsQuery, conn))
                            {
                                deleteCmd.Parameters.AddWithValue("@FileID", newFileId);
                                deleteCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string getMaxIdQuery = "SELECT ISNULL(MAX(FileID), 0) + 1 FROM FileStorage";
                            using (SqlCommand getMaxIdCmd = new SqlCommand(getMaxIdQuery, conn))
                            {
                                newFileId = (int)getMaxIdCmd.ExecuteScalar();
                            }
                        }
                    }

                    string insertQuery = "INSERT INTO FileStorage (FileID, FileName, FileType, FileData, CreatedAt) VALUES (@FileID, @FileName, @FileType, @FileData, SYSDATETIME())";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@FileID", newFileId);
                        insertCmd.Parameters.AddWithValue("@FileName", fileName);
                        insertCmd.Parameters.AddWithValue("@FileType", "application/octet-stream");
                        insertCmd.Parameters.AddWithValue("@FileData", fileData);
                        insertCmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }

                _inMemoryChunks.Remove(fileName);

                return Ok(new { message = "File " + fileName + " uploaded successfully.", fileId = newFileId });
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "Server error, please try again later." });
            }
        }
    }
}

