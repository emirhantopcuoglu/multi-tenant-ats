import type { ReactNode } from 'react';
import { useParams, useLocation, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AxiosError } from 'axios';
import { Badge, Button, Card, Skeleton } from '@/components/ui';
import { ThemeToggle } from '@/components/ThemeToggle';
import { LanguageSwitcher } from '@/components/LanguageSwitcher';
import { useAuth } from '@/app/auth/auth-context';
import { getInterviewRoom, type InterviewRoomState } from './interviewRoomApi';

const STATE_TONE: Record<InterviewRoomState, 'success' | 'warning' | 'neutral'> = {
  Open: 'success',
  TooEarly: 'warning',
  Ended: 'neutral',
  Unavailable: 'neutral',
};

function RoomShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <div className="flex min-h-screen flex-col bg-bg text-text">
      <header className="flex items-center justify-between border-b border-border px-6 py-4">
        <span className="font-semibold">{t('common.appName')}</span>
        <div className="flex items-center gap-3">
          <LanguageSwitcher />
          <ThemeToggle />
        </div>
      </header>
      <main className="mx-auto flex w-full max-w-lg flex-1 items-center justify-center px-6 py-10">
        {children}
      </main>
    </div>
  );
}

/* Landing page for the (future) live interview room link mailed to both the candidate and the
   interviewers. No video yet — this is the gated shell: it resolves the token, shows why the room
   isn't reachable when it isn't (too early / ended / cancelled), and will host the actual call once
   that infrastructure exists. Reachable by either a candidate or a company session; someone with
   neither is asked to sign in as whichever they are and is sent straight back here afterward. */
export function InterviewRoomPage() {
  const { t, i18n } = useTranslation();
  const { roomToken } = useParams<{ roomToken: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const { isAuthenticated, isLoading: authLoading } = useAuth();

  const query = useQuery({
    queryKey: ['interview-room', roomToken],
    queryFn: () => getInterviewRoom(roomToken!),
    enabled: Boolean(roomToken) && isAuthenticated,
    retry: false,
  });

  const dateFormatter = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long', timeStyle: 'short' });

  if (authLoading) {
    return (
      <RoomShell>
        <Skeleton className="h-40 w-full" />
      </RoomShell>
    );
  }

  if (!isAuthenticated) {
    return (
      <RoomShell>
        <Card className="w-full space-y-4 text-center">
          <h1 className="text-lg font-semibold">{t('interviewRoom.signInTitle')}</h1>
          <p className="text-sm text-text-muted">{t('interviewRoom.signInBody')}</p>
          <div className="flex flex-col gap-2 sm:flex-row sm:justify-center">
            <Button
              variant="secondary"
              className="w-full"
              onClick={() => navigate('/candidate/login', { state: { from: location } })}
            >
              {t('candidateAuth.publicSignIn')}
            </Button>
            <Button
              variant="secondary"
              className="w-full"
              onClick={() => navigate('/login', { state: { from: location } })}
            >
              {t('public.forCompanies')}
            </Button>
          </div>
        </Card>
      </RoomShell>
    );
  }

  if (query.isLoading) {
    return (
      <RoomShell>
        <Skeleton className="h-40 w-full" />
      </RoomShell>
    );
  }

  if (query.isError) {
    const notFound = query.error instanceof AxiosError && query.error.response?.status === 404;
    return (
      <RoomShell>
        <Card className="w-full space-y-2 text-center">
          <h1 className="text-lg font-semibold">
            {notFound ? t('interviewRoom.notFoundTitle') : t('interviewRoom.errorTitle')}
          </h1>
          <p className="text-sm text-text-muted">
            {notFound ? t('interviewRoom.notFoundBody') : t('interviewRoom.errorBody')}
          </p>
        </Card>
      </RoomShell>
    );
  }

  const room = query.data!;

  return (
    <RoomShell>
      <Card className="w-full space-y-4 text-center">
        <Badge tone={STATE_TONE[room.state]} dot>
          {t(`interviewRoom.state.${room.state}`)}
        </Badge>
        <div className="space-y-1">
          <h1 className="text-lg font-semibold">{room.jobTitle}</h1>
          <p className="text-sm text-text-muted">{t(`interviewType.${room.type}`)}</p>
          <p className="text-sm text-text-muted">{dateFormatter.format(new Date(room.scheduledAtUtc))}</p>
        </div>

        {room.state === 'Open' ? (
          <p className="text-sm text-text-muted">{t('interviewRoom.openBody')}</p>
        ) : room.state === 'TooEarly' ? (
          <p className="text-sm text-text-muted">
            {t('interviewRoom.tooEarlyBody', { time: dateFormatter.format(new Date(room.opensAtUtc)) })}
          </p>
        ) : room.state === 'Ended' ? (
          <p className="text-sm text-text-muted">{t('interviewRoom.endedBody')}</p>
        ) : (
          <p className="text-sm text-text-muted">{t('interviewRoom.unavailableBody')}</p>
        )}
      </Card>
    </RoomShell>
  );
}
