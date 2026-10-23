export type Profile = {
  id: string;
  email: string;
  displayName: string;
  university: string;
  roles: string[];
};
export type Tokens = { accessToken: string; refreshToken: string; expiresIn: number };
export type Course = {
  id: string;
  title: string;
  description?: string;
  isCurated: boolean;
  isRecognized: boolean;
  joined: boolean;
  materialCount: number;
  canRead?: boolean;
};
export type Material = {
  id: string;
  title: string;
  fileName: string;
  contentType: string;
  lessonId?: string;
  currentVersionId?: string;
  indexed?: boolean;
  pageCount?: number;
};
export type CourseDetail = Course & {
  materials: Material[];
  structure: { chapters?: { id: string; title: string }[] };
};
export type Citation = {
  chunkId: string;
  materialId: string;
  versionId: string;
  fileName: string;
  page: number;
  quote: string;
};
export type Paragraph = { heading?: string; text: string; citations: Citation[] };
export type Question = {
  id: string;
  question: string;
  options: { id: string; text: string }[];
  citations: Citation[];
};
export type StudyRequest = {
  kind: 'answer' | 'summary' | 'quiz';
  question?: string;
  materialId?: string;
  chapterId?: string;
  language: 'en' | 'fr';
  count?: 5 | 10;
};
export type Job = {
  title?: string;
  courseTitle?: string;
  scopeTitle?: string;
  id: string;
  courseId: string;
  kind: StudyRequest['kind'];
  state: string;
  stage: string;
  errorCode?: string;
  stale?: boolean;
  createdAt: string;
  request: StudyRequest;
  result?: {
    kind: string;
    insufficientEvidence: boolean;
    paragraphs?: Paragraph[];
    questions?: Question[];
    coveredPages: number;
    totalPages: number;
  };
};
export type Correction = Question & {
  questionId: string;
  selectedOptionId: string;
  correctOptionId: string;
  explanation: string;
};
export type Attempt = {
  title?: string;
  courseTitle?: string;
  id: string;
  jobId: string;
  courseId: string;
  createdAt: string;
  correct: number;
  total: number;
  corrections: Correction[];
};
export const pending = (state: string) => ['queued', 'running', 'retry_wait'].includes(state);
export type CommunityPost = {
  id: string;
  parentId?: string;
  courseId?: string;
  courseTitle?: string;
  title: string;
  body: string;
  kind: 'Question' | 'Study tip' | 'Discussion';
  isAnonymous: boolean;
  isSample: boolean;
  authorName: string;
  isMine: boolean;
  isOriginalPoster: boolean;
  score: number;
  myVote: number;
  replyCount: number;
  createdAt: string;
};
export type CommunityPage = { data: CommunityPost[]; total: number; page: number; limit: number };
