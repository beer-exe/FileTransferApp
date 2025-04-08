async function uploadFile() {
    let fileInput = document.getElementById('fileInput').files[0];
    if (!fileInput) {
        alert("Vui lòng chọn file!");
        return;
    }
    let formData = new FormData();
    formData.append("file", fileInput);

    let response = await fetch("http://localhost:5000/api/files/upload", {
        method: "POST",
        body: formData
    });

    let result = await response.json();
    alert(result.message);
}

function downloadFile() {
    let fileName = document.getElementById('fileName').value;
    if (!fileName) {
        alert("Nhập tên file để tải!");
        return;
    }
    window.location.href = "http://localhost:5000/api/files/download/" + fileName;
}