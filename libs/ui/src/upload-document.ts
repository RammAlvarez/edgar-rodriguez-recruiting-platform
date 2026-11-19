import { type ApiClient, uploadToPresignedUrl } from '@platform/api-client';
import type { components } from '@platform/shared';

type S = components['schemas'];

/** Presign → PUT straight to the bucket → confirm (ARCHITECTURE §4 "Files"): one document of a person's checklist. */
export async function uploadPersonDocument(
  api: ApiClient,
  personId: string,
  documentTypeId: string,
  file: File,
  onProgress?: (fraction: number) => void,
): Promise<S['PersonDocumentDto']> {
  const presign = await api.post<S['PresignUploadResponse']>('/v1/files/presign-upload', {
    personId,
    documentTypeId,
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
  });
  await uploadToPresignedUrl(file, { url: presign.url, headers: presign.headers }, onProgress);
  return api.post(`/v1/people/${personId}/documents`, {
    documentTypeId,
    storageKey: presign.storageKey,
    originalFilename: file.name,
    issuedAt: null,
    expiresAt: null,
  });
}
