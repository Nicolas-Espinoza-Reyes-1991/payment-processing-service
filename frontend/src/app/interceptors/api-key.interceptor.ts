import { HttpInterceptorFn } from '@angular/common/http';

const API_KEY = 'haulmer-demo-api-key-2026';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const cloned = req.clone({
    setHeaders: { 'X-Api-Key': API_KEY }
  });
  return next(cloned);
};
