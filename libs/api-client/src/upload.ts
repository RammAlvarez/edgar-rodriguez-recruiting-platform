/**
 * Uploads a file straight to object storage with the presigned PUT URL the API returned (ARCHITECTURE §4 "Files").
 * Uses XMLHttpRequest for progress (camera uploads on mobile); the API never streams files.
 */
export interface PresignedUpload {
  readonly url: string;
  readonly headers: Readonly<Record<string, string>>;
}

export function uploadToPresignedUrl(
  file: Blob,
  target: PresignedUpload,
  onProgress?: (fraction: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('PUT', target.url);
    for (const [name, value] of Object.entries(target.headers)) xhr.setRequestHeader(name, value);
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) onProgress?.(e.loaded / e.total);
    };
    xhr.onload = () =>
      xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new Error(`Upload failed (${xhr.status})`));
    xhr.onerror = () => reject(new Error('Upload failed'));
    xhr.send(file);
  });
}
