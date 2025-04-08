async function uploadFile() {
    let fileInput = document.getElementById('fileInput').files[0];
    if (!fileInput) {
        alert("Vui lòng chọn file!");
        return;
    }

    const maxFileSize = 1024 * 1024 * 1024; // 1GB
    if (fileInput.size > maxFileSize) {
        alert("Vui lòng chỉ tải lên file nhỏ hơn 1GB.");
        return;
    }

    const chunkSize = 5 * 1024 * 1024; // 5MB per chunk
    const totalChunks = Math.ceil(fileInput.size / chunkSize);
    const fileName = fileInput.name;

    for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {

        let start = chunkIndex * chunkSize;
        let end = Math.min(start + chunkSize, fileInput.size);
        let chunk = fileInput.slice(start, end);

        let checkResponse = await fetch(`http://171.248.83.156:5000/api/files/upload/check-chunk?fileName=${fileName}&chunkIndex=${chunkIndex}`);
        let checkResult = await checkResponse.json();

        if (checkResult.exists) {
            console.log(`Chunk ${chunkIndex} đã tồn tại, bỏ qua upload.`);
            continue;
        }

        // Nếu chunk chưa có, tiến hành upload
        let formData = new FormData();
        formData.append("chunk", chunk);
        formData.append("fileName", fileName);
        formData.append("chunkIndex", chunkIndex);
        formData.append("totalChunks", totalChunks);

        let uploadSuccess = false;
        let retries = 3; // Số lần thử lại nếu upload thất bại

        while (!uploadSuccess && retries > 0) {
            try {
                let response = await fetch("http://171.248.83.156:5000/api/files/upload/upload-chunk", {
                    method: "POST",
                    body: formData
                });

                if (response.ok) {
                    let result = await response.json();
                    console.log(result.message);
                    uploadSuccess = true;
                } else {
                    throw new Error("Upload chunk lỗi, thử lại...");
                }
            } catch (error) {
                console.error(`Lỗi upload chunk ${chunkIndex}:`, error);
                retries--;
                await new Promise(resolve => setTimeout(resolve, 2000)); // Đợi 2s trước khi thử lại
            }
        }

        if (!uploadSuccess) {
            alert(`Chunk ${chunkIndex} không thể upload sau nhiều lần thử!`);
            return;
        }
    }

    // Sau khi upload xong, gửi yêu cầu merge file
    let mergeFormData = new FormData();
    mergeFormData.append("fileName", fileName);
    mergeFormData.append("totalChunks", totalChunks);

    let mergeResponse = await fetch("http://171.248.83.156:5000/api/files/upload/merge-chunks", {
        method: "POST",
        body: mergeFormData
    });

    let mergeResult = await mergeResponse.json();
    alert(mergeResult.message);
}

async function downloadFile() {
    const fileName = document.getElementById("fileName").value;
    if (!fileName) {
        alert("Vui lòng nhập tên file!");
        return;
    }

    let checkUrl = `http://171.248.83.156:5000/api/files/download/check-file/${fileName}`;
    let response = await fetch(checkUrl);

    if (!response.ok) {
        let result = await response.json();
        alert(result.message || "Không tìm thấy file!");
        return;
    }

    window.location.href = `http://171.248.83.156:5000/api/files/download/download-file/${fileName}`;
}
