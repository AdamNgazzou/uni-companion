import React, { useEffect, useRef, useState } from 'react';
import { Asset } from 'expo-asset';
import { Notice } from '../ui';
import type { PdfProps } from './PdfCanvas';
export default function PdfCanvas({ data, page, zoom, onPage }: PdfProps) {
  const ref = useRef<HTMLIFrameElement>(null);
  const [html, setHtml] = useState('');
  const [ready, setReady] = useState(false);
  const [error, setError] = useState(false);
  const opened = useRef(false);
  const requestId = useRef(0);
  useEffect(() => {
    let active = true;
    fetch(Asset.fromModule(require('../../assets/pdf-viewer.html')).uri)
      .then((r) => r.text())
      .then((value) => {
        if (active) setHtml(value);
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    const receive = (event: MessageEvent) => {
      if (event.source !== ref.current?.contentWindow) return;
      try {
        const message = JSON.parse(event.data);
        if (message.type !== 'ready' && message.requestId !== requestId.current) return;
        if (message.type === 'ready') setReady(true);
        else if (message.type === 'error') setError(true);
        else if (
          message.type === 'page' &&
          Number.isSafeInteger(message.page) &&
          Number.isSafeInteger(message.total) &&
          message.page > 0 &&
          message.total >= message.page
        )
          onPage(message.page, message.total);
      } catch {}
    };
    window.addEventListener('message', receive);
    return () => window.removeEventListener('message', receive);
  }, [onPage]);
  useEffect(() => {
    if (!ready) return;
    ref.current?.contentWindow?.postMessage(
      JSON.stringify(
        opened.current
          ? { type: 'page', page, zoom, requestId: ++requestId.current }
          : { type: 'open', data, page, zoom, requestId: ++requestId.current },
      ),
      '*',
    );
    opened.current = true;
  }, [ready, data, page, zoom]);
  return error ? (
    <Notice tone="error">Couldn’t render this PDF. Reopen it or use the extracted text.</Notice>
  ) : (
    <iframe
      ref={ref}
      title="Original course PDF"
      srcDoc={html}
      sandbox="allow-scripts"
      style={{ flex: 1, minHeight: 0, border: 0, width: '100%' }}
    />
  );
}
