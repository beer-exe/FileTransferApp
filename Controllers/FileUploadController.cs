using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;

[Route("api/files/upload")]
[ApiController]
public class FileUploadController : ControllerBase
{
    private readonly string _uploadTempPath;
    private readonly string _finalUploadPath; 
    private readonly string _connectionString;

    public FileUploadController(IConfiguration configuration)
    {
        _connectionString = "Server=DESKTOP-IB2ECCK\\SQLEXPRESS;Database=FileStorageDB;Trusted_Connection=True;";
        _uploadTempPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Temp");
        _finalUploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Final");
        Directory.CreateDirectory(_uploadTempPath);
        Directory.CreateDirectory(_finalUploadPath);
    }

    [HttpPost("upload-chunk")]
    public async Task<IActionResult> UploadChunk([FromForm] IFormFile chunk, [FromForm] string fileName, [FromForm] int chunkIndex, [FromForm] int totalChunks)
    {
        if (chunk == null || chunk.Length == 0)
            return BadRequest("Chunk không hợp lệ.");

        string tempFilePath = Path.Combine(_uploadTempPath, $"{fileName}.part{chunkIndex}");

        using (var stream = new FileStream(tempFilePath, FileMode.Create))
        {
            await chunk.CopyToAsync(stream);
        }

        return Ok(new { message = $"Chunk {chunkIndex}/{totalChunks} uploaded successfully." });
    }

    [HttpGet("check-chunk")]
    public IActionResult CheckChunk([FromQuery] string fileName, [FromQuery] int chunkIndex)
    {
        string chunkPath = Path.Combine(_uploadTempPath, $"{fileName}.part{chunkIndex}");

        if (System.IO.File.Exists(chunkPath))
        {
            return Ok(new { exists = true });
        }
        return Ok(new { exists = false });
    }

    [HttpPost("merge-chunks")]
    public async Task<IActionResult> MergeChunks([FromForm] string fileName, [FromForm] int totalChunks)
    {
        string finalFilePath = Path.Combine(_finalUploadPath, fileName);

        using (FileStream finalFileStream = new FileStream(finalFilePath, FileMode.Create))
        {
            for (int i = 0; i < totalChunks; i++)
            {
                string chunkPath = Path.Combine(_uploadTempPath, $"{fileName}.part{i}");

                if (!System.IO.File.Exists(chunkPath))
                    return BadRequest($"Chunk {i} missing!");

                byte[] chunkData = await System.IO.File.ReadAllBytesAsync(chunkPath);
                await finalFileStream.WriteAsync(chunkData, 0, chunkData.Length);

                System.IO.File.Delete(chunkPath); 
            }
        }

        int newFileId;
        byte[] fileData = await System.IO.File.ReadAllBytesAsync(finalFilePath);

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
        System.IO.File.Delete(finalFilePath);

        return Ok(new { message = "File"+ fileName +" tải lên thành công.", fileId = newFileId });
    }
}