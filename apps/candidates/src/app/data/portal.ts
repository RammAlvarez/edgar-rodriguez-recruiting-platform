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
export type BankAccountDto = S['BankAccountDto'];
export type BankAccountRequest = S['BankAccountRequest'];
export type BankOptionDto = S['BankOptionDto'];
export type BankAccountType = NonNullable<S['BankAccountType']>;

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

  /** «Cuenta para tu pago» (PLAN D77 → S7): only the last four digits ever come back. */
  bankAccounts() {
    return liveQuery(() => this.api.get<BankAccountDto[]>('/v1/candidate-portal/bank-accounts'), ['candidate-portal']);
  }

  banks(): Promise<BankOptionDto[]> {
    return this.api.get('/v1/candidate-portal/banks');
  }

  addBankAccount(body: BankAccountRequest): Promise<BankAccountDto[]> {
    return this.api.post('/v1/candidate-portal/bank-accounts', body);
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
