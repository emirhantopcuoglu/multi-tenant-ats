import { Avatar } from '@/components/ui';
import { fullName, useUserLookup } from '@/features/users/useUsers';

interface InterviewerAvatarsProps {
  interviewerUserIds: string[];
  /** How many avatars to render before collapsing the rest into a "+N" token. */
  max?: number;
}

/* Overlapping avatar stack for an interview's panel. Resolves each id to a tenant member (loaded once
   via useUserLookup) so the avatar shows real initials and a name tooltip; ids not yet resolved fall
   back to a neutral placeholder rather than disappearing. */
export function InterviewerAvatars({ interviewerUserIds, max = 4 }: InterviewerAvatarsProps) {
  const lookup = useUserLookup();
  const shown = interviewerUserIds.slice(0, max);
  const overflow = interviewerUserIds.length - shown.length;

  if (interviewerUserIds.length === 0) {
    return <span className="text-text-muted">—</span>;
  }

  return (
    <div className="flex items-center -space-x-1.5">
      {shown.map((id) => {
        const user = lookup.get(id);
        return (
          <Avatar
            key={id}
            name={user ? fullName(user) : '?'}
            size="sm"
            className="ring-2 ring-card"
          />
        );
      })}
      {overflow > 0 && (
        <span className="ml-2.5 text-xs font-medium text-text-muted">+{overflow}</span>
      )}
    </div>
  );
}
