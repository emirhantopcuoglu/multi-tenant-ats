import http from 'k6/http';
import { check, sleep } from 'k6';

// The public pages: anyone can load them without an account, so they take the most traffic. Every
// request comes from one IP here, so raise the public limit first or most of them are refused (README).
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5236';
const SEARCHES = ['engineer', 'designer', 'data', 'zzq'];

export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '1m', target: 50 },
    { duration: '15s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

export default function () {
  const search = SEARCHES[Math.floor(Math.random() * SEARCHES.length)];
  const responses = http.batch([
    ['GET', `${BASE_URL}/public/jobs?page=1&pageSize=20`],
    ['GET', `${BASE_URL}/public/jobs?search=${search}`],
    ['GET', `${BASE_URL}/public/companies`],
  ]);
  for (const response of responses)
    check(response, { 'status is 200': (r) => r.status === 200 });

  // A person reads the page before the next click.
  sleep(1);
}
