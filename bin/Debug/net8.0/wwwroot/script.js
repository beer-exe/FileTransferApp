const API_BASE = 'http://115.77.92.93:5000/api/files';
const maxFileSize = 1024 * 1024 * 1024; // 1GB
const chunkSize = 5 * 1024 * 1024; // 5MB

async function uploadFile() 
{
    let fileInput = document.getElementById('fileInput').files[0];
    
    if (!fileInput) 
    {
        alert("Vui lòng chọn file!");
        return;
    }

    if (fileInput.size > maxFileSize) 
    {
        alert("Vui lòng chỉ tải lên file nhỏ hơn 1GB.");
        return;
    }

    const progressBar = document.getElementById("progressBar");
    document.getElementById("progressBar").style.display = 'block';

    const totalChunks = Math.ceil(fileInput.size / chunkSize);
    const fileName = fileInput.name;

    for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) 
    {
        let start = chunkIndex * chunkSize;
        let end = Math.min(start + chunkSize, fileInput.size);
        let chunk = fileInput.slice(start, end);

        let checkResponse = await fetch(`${API_BASE}/upload/check-chunk?fileName=${fileName}&chunkIndex=${chunkIndex}`);
        let checkResult = await checkResponse.json();

        if (checkResult.exists) 
        {
            console.log(`Chunk ${chunkIndex} đã tồn tại, bỏ qua upload.`);
            continue;
        }

        let formData = new FormData();
        formData.append("chunk", chunk);
        formData.append("fileName", fileName);
        formData.append("chunkIndex", chunkIndex);
        formData.append("totalChunks", totalChunks);

        let uploadSuccess = false;
        let retries = 3;

        while (!uploadSuccess && retries > 0) 
        {
            try 
            {
                await new Promise((resolve, reject) => 
                {
                    const xhr = new XMLHttpRequest();
                    xhr.open("POST", `${API_BASE}/upload/upload-chunk`, true);

                    xhr.upload.onprogress = function (e) 
                    {
                        if (e.lengthComputable) 
                        {
                            const totalUploaded = (chunkIndex * chunkSize + e.loaded);
                            const percentComplete = (totalUploaded / fileInput.size) * 100;
                            progressBar.value = percentComplete;
                        }
                    };

                    xhr.onload = function () 
                    {
                        if (xhr.status === 200) 
                        {
                            const result = JSON.parse(xhr.responseText);
                            console.log(result.message);
                            uploadSuccess = true;
                            resolve();
                        } 
                        else 
                        {
                            reject("Lỗi từ server.");
                        }
                    };

                    xhr.onerror = function () 
                    {
                        reject("Lỗi mạng.");
                    };

                    xhr.send(formData);
                });
            } 
            catch (error) 
            {
                console.error(`Lỗi upload chunk ${chunkIndex}:`, error);
                retries--;
                await new Promise(resolve => setTimeout(resolve, 2000));
            }
        }

        if (!uploadSuccess) {
            alert(`Chunk ${chunkIndex} không thể upload sau nhiều lần thử!`);
            return;
        }
    }

    // Merge file sau khi upload xong
    let mergeFormData = new FormData();
    mergeFormData.append("fileName", fileName);
    mergeFormData.append("totalChunks", totalChunks);

    let mergeResponse = await fetch(`${API_BASE}/upload/merge-chunks`, 
    {
        method: "POST",
        body: mergeFormData
    });

    let mergeResult = await mergeResponse.json();
    alert(mergeResult.message);
    progressBar.value = 0;
    document.getElementById("progressBar").style.display = 'none';

}


async function downloadFile() {
    const fileName = document.getElementById("fileName").value;
    if (!fileName) {
        alert("Vui lòng nhập tên file!");
        return;
    }

    let checkUrl = `${API_BASE}/download/check-file/${fileName}`;
    let response = await fetch(checkUrl);

    if (!response.ok) {
        let result = await response.json();
        alert(result.message || "Không tìm thấy file!");
        return;
    }

    window.location.href = `${API_BASE}/download/download-file/${fileName}`;
}

async function toggleFileList(button) {
    const container = document.getElementById("fileListContainer");
    const isVisible = container.style.display === "block";

    if (!isVisible) {
        try {
            // Gọi API backend
            const response = await fetch(API_BASE);
            const files = await response.json();

            const tbody = document.getElementById("fileTable").querySelector("tbody");
            tbody.innerHTML = "";

            files.forEach(file => {
                const row = document.createElement("tr");
                row.innerHTML = `
                    <td>${file.fileID}</td>
                    <td>${file.fileName}</td>
                    <td>${new Date(file.createdAt).toLocaleString()}</td>
                `;
                tbody.appendChild(row);
            });

            container.style.display = "block";
            button.textContent = "Hide";
        } catch (error) {
            alert("Không thể tải danh sách file từ server.");
            console.error(error);
        }
    } else {
        container.style.display = "none";
        button.textContent = "Show";
    }
}

