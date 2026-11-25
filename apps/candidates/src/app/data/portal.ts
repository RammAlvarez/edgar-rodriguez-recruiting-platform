import { inject, Injectable } from '@angular/core';
import type { components } from '@platform/shared';
import { API_CLIENT } from '@platform/ui/api';
import { uploadPersonDocument } from '@platform/ui/upload-document';
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

  upload(personId: string, item: ChecklistItemDto, file: File, onProgress?: (f: number) => void): Promise<PersonDocumentDto> {
    return uploadPersonDocument(this.api, personId, item.documentTypeId, file, onProgress);
  }

  url(documentId: string): Promise<DocumentUrlDto> {
    return this.api.get(`/v1/documents/${documentId}/url`);
  }
}
