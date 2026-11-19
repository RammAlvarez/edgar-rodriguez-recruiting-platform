import { describe, expect, it, vi } from 'vitest';

const uploadToPresignedUrl = vi.fn(async () => undefined);
vi.mock('@platform/api-client', () => ({ uploadToPresignedUrl }));

const { uploadPersonDocument } = await import('./upload-document');

describe('uploadPersonDocument', () => {
  it('presigns, PUTs the file to the returned URL, then confirms the document', async () => {
    const calls: [string, unknown][] = [];
    const api = {
      post: vi.fn(async (path: string, body?: unknown) => {
        calls.push([path, body]);
        return path === '/v1/files/presign-upload'
          ? { url: 'https://bucket.test/put', headers: { 'x-amz-acl': 'private' }, storageKey: 'documents/abc' }
          : { id: 'doc-1' };
      }),
    };
    const file = new File(['pdf'], 'ine.pdf', { type: 'application/pdf' });
    const progress = vi.fn();

    const result = await uploadPersonDocument(api as never, 'person-1', 'type-1', file, progress);

    expect(result).toEqual({ id: 'doc-1' });
    expect(calls[0]).toEqual([
      '/v1/files/presign-upload',
      { personId: 'person-1', documentTypeId: 'type-1', fileName: 'ine.pdf', contentType: 'application/pdf', sizeBytes: 3 },
    ]);
    expect(uploadToPresignedUrl).toHaveBeenCalledWith(file, { url: 'https://bucket.test/put', headers: { 'x-amz-acl': 'private' } }, progress);
    expect(calls[1]).toEqual([
      '/v1/people/person-1/documents',
      { documentTypeId: 'type-1', storageKey: 'documents/abc', originalFilename: 'ine.pdf', issuedAt: null, expiresAt: null },
    ]);
  });

  it('does not confirm a document when the upload to the bucket fails', async () => {
    uploadToPresignedUrl.mockRejectedValueOnce(new Error('Upload failed (403)'));
    const api = { post: vi.fn(async () => ({ url: 'u', headers: {}, storageKey: 'k' })) };
    await expect(uploadPersonDocument(api as never, 'p', 't', new File(['x'], 'a.pdf'))).rejects.toThrow('Upload failed');
    expect(api.post).toHaveBeenCalledTimes(1);
  });
});
