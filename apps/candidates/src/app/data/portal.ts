import { inject, Injectable } from '@angular/core';
import { uploadToPresignedUrl } from '@platform/api-client';
import type { components } from '@platform/shared';
import { API_CLIENT } from '@platform/ui/api';
import { liveQuery } from '@platform/ui/live-query';

type S = components['schemas'];
export type CandidateProfileDto = S['CandidateProfileDto'];
export type CandidateProfileRequest = S['CandidateProfileRequest'];
export type CandidateApplicationDto = S['CandidateApplicationDto'];
export type ApplicationStep = S['ApplicationStep'];
export type ApplicationOutcome = S['ApplicationOutcome'];
export type DocumentChecklistDto = S['DocumentChecklistDto'];
export type ChecklistItemDto = S['ChecklistItemDto'];
export type PersonDocumentDto = S['PersonDocumentDto'];
type PresignUploadResponse = S['PresignUploadResponse'];
type DocumentUrlDto = S['DocumentUrlDto'];

/**
 * The candidate's own data (`/v1/candidate-portal`, PLAN D77). Uploads and views go through the shared document routes,
 * which only let a candidate reach their own person.
 */
@Injectable({ providedIn: 'root' })
export class CandidatePortal {
  private readonly api = inject(API_CLIENT);

  profile() {
    return liveQuery(() => this.api.get<CandidateProfileDto>('/v1/candidate-portal/profile'), ['candidate-portal', 'people']);
  }

  getProfile(): Promise<CandidateProfileDto> {
    return this.api.get('/v1/candidate-portal/profile');
  }

  saveProfile(body: CandidateProfileRequest): Promise<CandidateProfileDto> {
    return this.api.put('/v1/candidate-portal/profile', body);
  }

  acceptPrivacyNotice(): Promise<CandidateProfileDto> {
    return this.api.post('/v1/candidate-portal/privacy-notice');
  }

  applications() {
    return liveQuery(
      () => this.api.get<CandidateApplicationDto[]>('/v1/candidate-portal/applications'),
      ['candidate-portal', 'candidates'],
    );
  }

  documents() {
    return liveQuery(() => this.api.get<DocumentChecklistDto>('/v1/candidate-portal/documents'), ['documents', 'candidate-portal']);
  }

  async upload(personId: string, item: ChecklistItemDto, file: File, onProgress?: (f: number) => void): Promise<PersonDocumentDto> {
    const presign = await this.api.post<PresignUploadResponse>('/v1/files/presign-upload', {
      personId,
      documentTypeId: item.documentTypeId,
      fileName: file.name,
      contentType: file.type,
      sizeBytes: file.size,
    });
    await uploadToPresignedUrl(file, { url: presign.url, headers: presign.headers }, onProgress);
    return this.api.post(`/v1/people/${personId}/documents`, {
      documentTypeId: item.documentTypeId,
      storageKey: presign.storageKey,
      originalFilename: file.name,
      issuedAt: null,
      expiresAt: null,
    });
  }

  url(documentId: string): Promise<DocumentUrlDto> {
    return this.api.get(`/v1/documents/${documentId}/url`);
  }
}
