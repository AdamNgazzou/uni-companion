import React, { useEffect, useRef, useState } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { Asset } from 'expo-asset';
import { File } from 'expo-file-system';
import { WebView } from 'react-native-webview';
import { Notice } from '../ui';
import { colors as c } from '../theme';
export type PdfProps = {
  data: string;
  page: number;
  zoom: number;
  onPage: (page: number, total: number) => void;
};
export default function PdfCanvas({ data, page, zoom, onPage }: PdfProps) {
  const ref = useRef<WebView<object>>(null);
  const [html, setHtml] = useState('');
  const [error, setError] = useState(false);
  const [ready, setReady] = useState(false);
  const opened = useRef(false);
  const requestId = useRef(0);
  useEffect(() => {
    let active = true;
    Asset.fromModule(require('../../assets/pdf-viewer.html'))
      .downloadAsync()
      .then(async (asset) => {
        if (!asset.localUri) throw new Error('Missing PDF renderer');
        const text = await new File(asset.localUri).text();
        if (active) setHtml(text);
      })
      .catch(() => {
        if (active) setError(true);
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    if (ready && !opened.current) {
      opened.current = true;
      ref.current?.postMessage(
        JSON.stringify({ type: 'open', data, page, zoom, requestId: ++requestId.current }),
      );
    } else if (ready)
      ref.current?.postMessage(
        JSON.stringify({ type: 'page', page, zoom, requestId: ++requestId.current }),
      );
  }, [ready, data, page, zoom]);
  if (error)
    return (
      <Notice tone="error">
        Couldn’t render this PDF. Go back and reopen it, or use the extracted page text below.
      </Notice>
    );
  if (!html) return <ActivityIndicator color={c.blue} />;
  return (
    <View style={{ flex: 1, minHeight: 0 }}>
      <WebView<object>
        ref={ref}
        source={{ html, baseUrl: 'about:blank' }}
        originWhitelist={['about:blank']}
        javaScriptEnabled
        allowFileAccess={false}
        allowFileAccessFromFileURLs={false}
        allowUniversalAccessFromFileURLs={false}
        mixedContentMode="never"
        setSupportMultipleWindows={false}
        domStorageEnabled={false}
        thirdPartyCookiesEnabled={false}
        sharedCookiesEnabled={false}
        onShouldStartLoadWithRequest={(request) => request.url === 'about:blank'}
        onError={() => setError(true)}
        onMessage={(event) => {
          try {
            const message = JSON.parse(event.nativeEvent.data);
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
          } catch {
            setError(true);
          }
        }}
      />
    </View>
  );
}
