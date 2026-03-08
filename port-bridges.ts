import express from "express";
import type { Request, Response } from "express";

async function handleProxy(req: Request, res: Response) {
  try {
    const isApi = req.url.startsWith("/api");
    if (isApi) {
      const target = `http://localhost:3004${req.url}`;
      const upstream = await fetch(target, {
        method: req.method,
        headers: {
          ...req.headers,
          host: "localhost:3004",
        } as any,
        body:
          req.method === "GET" || req.method === "HEAD"
            ? undefined
            : (req as any),
        signal: AbortSignal.timeout(60000),
      } as any);
      res.status(upstream.status);
      upstream.headers.forEach((v, k) => {
        res.setHeader(k, v);
      });
      if (upstream.body) {
        (upstream.body as any).pipe(res);
      } else {
        res.end();
      }
      return;
    }
    const uiUrl = `http://localhost:3003${req.url}`;
    res.redirect(302, uiUrl);
  } catch (e: any) {
    res.status(502).send("Bridge error");
  }
}

function createBridgeServer(port: number) {
  const app = express();
  app.use(handleProxy);
  app.listen(port, () => {
    console.log(`Bridge server listening on http://localhost:${port} → UI:3003, API:3004`);
  });
}

createBridgeServer(7113);
createBridgeServer(3803);
