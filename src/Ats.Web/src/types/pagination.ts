/* Mirrors the backend PagedResult<T> (each module's Application/PagedResult.cs).
   Field names verified against the source: the count is `totalCount`, not `total`, and `totalPages`
   is a server-computed convenience field. List endpoints accept `page`/`pageSize` query params. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/* Common query shape for paginated list endpoints. Screen-specific filters extend this. */
export interface PageQuery {
  page?: number;
  pageSize?: number;
}
