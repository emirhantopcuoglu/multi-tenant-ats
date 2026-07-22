import { useQuery } from '@tanstack/react-query';
import { getPublicCompany } from './publicCompanyApi';

export function usePublicCompany(slug: string) {
  return useQuery({
    queryKey: ['public', 'company', slug],
    queryFn: () => getPublicCompany(slug),
    enabled: slug.length > 0,
  });
}
