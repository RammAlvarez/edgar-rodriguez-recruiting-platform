import { inject, Injectable } from '@angular/core';
import { API_CLIENT, liveQuery, uploadPersonDocument } from '@platform/ui';
import type {
  ExpiringDocumentDto,
  AssignmentRole,
  CandidateDetailDto,
  CandidateInvitationResultDto,
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
  PersonDto,
  PersonSummaryDto,
  RecruiterDetailDto,
  RecruiterSummaryDto,
  StaffOptionDto,
  TimelineEntryDto,
} from './api-types';
import type { CandidateStage, OpeningStatus } from './recruiting-labels';

/** Required documents of a checklist not delivered yet: missing, rejected or expired (a delivered one awaits validation). */
export function missingRequired(list: Pick<DocumentChecklistDto, 'items'>): number {
  return list.items.filter((i) => i.inTemplate && i.isRequired && !['delivered', 'validated'].includes(i.current?.status ?? 'pending'))
    .length;
}

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

  /** «Reenviar invitación» to the Candidates app (PLAN D77). */
  invite(id: string): Promise<CandidateInvitationResultDto> {
    return this.api.post(`/v1/candidates/${id}/invitation`);
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

  /**
   * How many required documents of the candidate's checklist are not delivered yet (neither Entregado nor Validado); the
   * hire dialog warns with it but never blocks (owner, 2026-11-25).
   */
  async missingRequiredDocuments(id: string): Promise<number> {
    const c = await this.api.get<CandidateDetailDto>(`/v1/candidates/${id}`);
    const list = await this.api.get<DocumentChecklistDto>(
      `/v1/people/${c.person.id}/documents`,
      c.documentTemplateId ? { templateId: c.documentTemplateId } : undefined,
    );
    return missingRequired(list);
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

  get(id: string): Promise<PersonDto> {
    return this.api.get(`/v1/people/${id}`);
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

  upload(personId: string, item: ChecklistItemDto, file: File, onProgress?: (f: number) => void): Promise<PersonDocumentDto> {
    return uploadPersonDocument(this.api, personId, item.documentTypeId, file, onProgress);
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

/** Inicio: the documents about to expire (the counts come from the lists the other screens read). */
@Injectable({ providedIn: 'root' })
export class DashboardRepository {
  private readonly api = inject(API_CLIENT);

  expiringDocuments() {
    return liveQuery(() => this.api.get<ExpiringDocumentDto[]>('/v1/dashboard/expiring-documents'), ['people']);
  }
}
