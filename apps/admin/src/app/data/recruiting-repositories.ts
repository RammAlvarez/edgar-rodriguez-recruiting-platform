import { inject, Injectable } from '@angular/core';
import { uploadToPresignedUrl } from '@platform/api-client';
import { API_CLIENT, liveQuery } from '@platform/ui';
import type {
  AssignmentRole,
  CandidateDetailDto,
  CandidateListItemDto,
  ChecklistItemDto,
  CreateCandidateRequest,
  DocumentChecklistDto,
  InterviewDto,
  InterviewRequest,
  MoveStageResultDto,
  NoteDto,
  OpeningCommentDto,
  OpeningDetailDto,
  OpeningEventDto,
  OpeningListItemDto,
  OpeningOptionDto,
  OpeningRequest,
  PersonDocumentDto,
  PersonSummaryDto,
  PresignUploadResponse,
  RecruiterDetailDto,
  RecruiterSummaryDto,
  StaffOptionDto,
  TimelineEntryDto,
} from './api-types';
import type { CandidateStage, OpeningStatus } from './recruiting-labels';

/**
 * Reclutamiento's calls to `/v1`. Every list is already scoped by the API (a Reclutador, or cover mode via X-Acting-For,
 * only gets her vacantes); queries are live and re-run on the tags the mutations return.
 */
@Injectable({ providedIn: 'root' })
export class OpeningsRepository {
  private readonly api = inject(API_CLIENT);

  list(clientId?: () => string | null) {
    return liveQuery(
      () => this.api.get<OpeningListItemDto[]>('/v1/openings', clientId?.() ? { clientId: clientId()! } : undefined),
      ['openings'],
    );
  }

  options() {
    return liveQuery(() => this.api.get<OpeningOptionDto[]>('/v1/openings/options'), ['openings']);
  }

  detail(id: () => string) {
    return liveQuery(() => this.api.get<OpeningDetailDto>(`/v1/openings/${id()}`), ['openings', 'candidates']);
  }

  events(id: () => string) {
    return liveQuery(() => this.api.get<OpeningEventDto[]>(`/v1/openings/${id()}/events`), ['openings', 'candidates']);
  }

  comments(id: () => string) {
    return liveQuery(() => this.api.get<OpeningCommentDto[]>(`/v1/openings/${id()}/comments`), ['openings']);
  }

  /** A one-off read (dialogs), not a live query. */
  get(id: string): Promise<OpeningDetailDto> {
    return this.api.get(`/v1/openings/${id}`);
  }

  save(id: string | null, body: OpeningRequest): Promise<OpeningDetailDto> {
    return id ? this.api.put(`/v1/openings/${id}`, body) : this.api.post('/v1/openings', body);
  }

  changeStatus(id: string, status: OpeningStatus, reason: string | null): Promise<OpeningDetailDto> {
    return this.api.post(`/v1/openings/${id}/status`, { status, reason });
  }

  duplicate(id: string): Promise<OpeningDetailDto> {
    return this.api.post(`/v1/openings/${id}/duplicate`);
  }

  assign(id: string, userIds: string[], role: AssignmentRole): Promise<OpeningDetailDto> {
    return this.api.post(`/v1/openings/${id}/recruiters`, { userIds, role });
  }

  makeLead(id: string, userId: string): Promise<OpeningDetailDto> {
    return this.api.post(`/v1/openings/${id}/recruiters/${userId}/lead`);
  }

  remove(id: string, userId: string, newLeadId: string | null): Promise<OpeningDetailDto> {
    return this.api.post(`/v1/openings/${id}/recruiters/${userId}/remove`, { newLeadId });
  }

  postComment(id: string, body: string, visibleToClient: boolean): Promise<OpeningCommentDto> {
    return this.api.post(`/v1/openings/${id}/comments`, { body, visibleToClient });
  }
}

@Injectable({ providedIn: 'root' })
export class RecruitersRepository {
  private readonly api = inject(API_CLIENT);

  list() {
    return liveQuery(() => this.api.get<RecruiterSummaryDto[]>('/v1/recruiters'), ['recruiters', 'users']);
  }

  detail(id: () => string) {
    return liveQuery(() => this.api.get<RecruiterDetailDto>(`/v1/recruiters/${id()}`), ['recruiters', 'openings', 'users']);
  }

  staff() {
    return liveQuery(() => this.api.get<StaffOptionDto[]>('/v1/staff'), ['users']);
  }

  assignOpenings(id: string, openingIds: string[], role: AssignmentRole): Promise<RecruiterDetailDto> {
    return this.api.post(`/v1/recruiters/${id}/openings`, { openingIds, role });
  }
}

@Injectable({ providedIn: 'root' })
export class CandidatesRepository {
  private readonly api = inject(API_CLIENT);

  list(openingId?: () => string | null) {
    return liveQuery(
      () => this.api.get<CandidateListItemDto[]>('/v1/candidates', openingId?.() ? { openingId: openingId()! } : undefined),
      ['candidates'],
    );
  }

  detail(id: () => string) {
    return liveQuery(() => this.api.get<CandidateDetailDto>(`/v1/candidates/${id()}`), ['candidates', 'people']);
  }

  history(id: () => string) {
    return liveQuery(() => this.api.get<TimelineEntryDto[]>(`/v1/candidates/${id()}/history`), ['candidates']);
  }

  notes(id: () => string) {
    return liveQuery(() => this.api.get<NoteDto[]>(`/v1/candidates/${id()}/notes`), ['candidates']);
  }

  interviews(id: () => string) {
    return liveQuery(() => this.api.get<InterviewDto[]>(`/v1/candidates/${id()}/interviews`), ['candidates']);
  }

  create(body: CreateCandidateRequest): Promise<CandidateDetailDto> {
    return this.api.post('/v1/candidates', body);
  }

  update(id: string, body: { source: string; offeredSalary: string | null; comments: string | null }): Promise<CandidateDetailDto> {
    return this.api.put(`/v1/candidates/${id}`, body);
  }

  move(id: string, stage: CandidateStage, reason: string | null, hiredAt: string | null): Promise<MoveStageResultDto> {
    return this.api.post(`/v1/candidates/${id}/stage`, { stage, reason, hiredAt });
  }

  saveNote(id: string, noteId: string | null, contentHtml: string, contentJson: string | null): Promise<NoteDto> {
    const body = { contentHtml, contentJson };
    return noteId ? this.api.put(`/v1/candidates/${id}/notes/${noteId}`, body) : this.api.post(`/v1/candidates/${id}/notes`, body);
  }

  deleteNote(id: string, noteId: string): Promise<void> {
    return this.api.delete(`/v1/candidates/${id}/notes/${noteId}`);
  }

  saveInterview(id: string, interviewId: string | null, body: InterviewRequest): Promise<InterviewDto> {
    return interviewId
      ? this.api.put(`/v1/candidates/${id}/interviews/${interviewId}`, body)
      : this.api.post(`/v1/candidates/${id}/interviews`, body);
  }
}

/** The people registry and document vault as the candidate screens use them (SPEC §3.1–3.2). */
@Injectable({ providedIn: 'root' })
export class PeopleRepository {
  private readonly api = inject(API_CLIENT);

  search(text: string): Promise<PersonSummaryDto[]> {
    return this.api.get('/v1/people', { search: text });
  }

  /** Resolves to undefined while the person is not known yet; call `reload()` once it is. */
  checklist(personId: () => string | null, templateId: () => string | null) {
    return liveQuery(
      () => {
        const id = personId();
        return id
          ? this.api.get<DocumentChecklistDto | undefined>(`/v1/people/${id}/documents`, templateId() ? { templateId: templateId()! } : undefined)
          : Promise.resolve(undefined);
      },
      ['documents', 'people'],
    );
  }

  /** Presign → PUT straight to the bucket → confirm (ARCHITECTURE §4 "Files"). */
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

  validate(documentId: string): Promise<PersonDocumentDto> {
    return this.api.post(`/v1/documents/${documentId}/validate`);
  }

  reject(documentId: string, reason: string): Promise<PersonDocumentDto> {
    return this.api.post(`/v1/documents/${documentId}/reject`, { reason });
  }

  url(documentId: string, download: boolean): Promise<{ url: string }> {
    return this.api.get(`/v1/documents/${documentId}/url`, download ? { download: 'true' } : undefined);
  }
}
