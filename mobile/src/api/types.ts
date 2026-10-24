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
export type Paragraph = {
  heading?: string;
  text: string;
  citations: Citation[];
  basis?: 'saved_work' | 'course_sources';
};
export type Question = {
  id: string;
  question: string;
  options: { id: string; text: string }[];
  citations: Citation[];
};
export type StudyRequest = {
  kind: 'answer' | 'summary' | 'quiz' | 'practice' | 'tutor';
  question?: string;
  conversationId?: string | null;
  materialId?: string;
  chapterId?: string;
  language: 'en' | 'fr';
  count?: 5 | 10;
  practice?: {
    mode: 'written' | 'coding' | 'mixed';
    difficulty: 'guided' | 'standard' | 'challenge';
    minutes: 15 | 30 | 45;
  };
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
    practice?: PracticeArtifact;
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
  editedAt?: string;
  isDeleted: boolean;
  acceptedReplyId?: string;
  score: number;
  myVote: number;
  replyCount: number;
  createdAt: string;
};
export type CommunityPage = { data: CommunityPost[]; total: number; page: number; limit: number };
export type SavedItem = {
  id: string;
  kind: 'thread' | 'material';
  targetId: string;
  title: string;
  courseId?: string;
  currentVersionId?: string;
  contentType?: string;
};
export type MyDiscussionPage = {
  data: { post: CommunityPost; threadId: string; threadTitle: string; replyPage: number }[];
  total: number;
  page: number;
  limit: number;
};
export type Report = {
  id: string;
  postId: string;
  threadId: string;
  title: string;
  body: string;
  reason: string;
  createdAt: string;
};
export type ActivityItem = {
  id: string;
  kind: 'reply' | 'upvote' | 'study_completed' | 'study_failed';
  title: string;
  detail: string;
  createdAt: string;
  updatedAt: string;
  readAt?: string;
};
export type ActivityPage = {
  data: ActivityItem[];
  total: number;
  unreadCount: number;
  page: number;
  limit: number;
};
export type ActivityPreferences = {
  replies: boolean;
  followedThreads: boolean;
  studyJobs: boolean;
  upvotes: boolean;
};
export type ActivityDestination = {
  kind: 'study' | 'thread';
  id: string;
  replyId?: string;
  replyPage?: number;
};

export type PracticeTask = {
  id: string;
  kind: 'written' | 'coding';
  title: string;
  prompt: string;
  given: string;
  citations: Citation[];
  starterCode?: string | null;
  runInstructions?: string | null;
  expectedOutput?: string | null;
  steps: string[];
  checks: { input: string; expected: string }[];
};
export type PracticeArtifact = {
  mode: 'written' | 'coding' | 'mixed';
  language: string;
  objectives: string[];
  prerequisites: string[];
  tasks: PracticeTask[];
};
export type PracticeState = {
  id: string;
  hintLevel: number;
  hints: string[];
  hintCount: number;
  solutionViewed: boolean;
  solution?: string | null;
  solutionCode?: string | null;
  rubric?: string[] | null;
  attempts: {
    id: string;
    answer: string;
    hintsUsed: boolean;
    solutionViewedBefore: boolean;
    createdAt: string;
  }[];
};

export type TutorTurn = {
  id: string;
  ordinal: number;
  question: string;
  action: 'explain' | 'hint' | 'check';
  createdAt: string;
  jobId: string;
  state: string;
  stage: string;
  errorCode?: string | null;
  result?: Job['result'] | null;
};
export type TutorConversation = {
  id: string;
  title: string;
  courseId: string;
  courseTitle: string;
  scopeTitle: string;
  language: 'en' | 'fr';
  isFeedback: boolean;
  practice?: {
    jobId: string;
    taskId: string;
    attemptId: string;
    answer: string;
    usesEarlierSources: boolean;
  } | null;
  turns: TutorTurn[];
  turnLimit: number;
};
export type TutorPage = {
  data: {
    id: string;
    title: string;
    courseId: string;
    courseTitle: string;
    language: string;
    updatedAt: string;
    isFeedback: boolean;
  }[];
  total: number;
  page: number;
  limit: number;
};
