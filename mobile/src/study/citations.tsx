import React, { useState } from 'react';
import { Pressable, View } from 'react-native';
import { router } from 'expo-router';
import type { Citation } from '../api/types';
import { Icon, T } from '../ui';
import { colors as c } from '../theme';
import { sourceName } from './presentation';

export function Citations({ citations }: { citations: Citation[] }) {
  const [expanded, setExpanded] = useState(false);
  const pages = [
    ...new Map(citations.map((source) => [source.versionId + ':' + source.page, source])).values(),
  ];
  if (!pages.length) return null;
  return (
    <View style={{ gap: 6 }}>
      {pages.map((source) => (
        <Pressable
          key={source.versionId + source.page}
          accessibilityRole="button"
          accessibilityLabel={`Open ${sourceName(source.fileName)}, PDF page ${source.page}`}
          onPress={() =>
            router.push({
              pathname: '/reader',
              params: {
                materialId: source.materialId,
                versionId: source.versionId,
                page: String(source.page),
              },
            })
          }
          style={({ pressed }) => ({
            minHeight: 44,
            flexDirection: 'row',
            alignItems: 'center',
            gap: 9,
            opacity: pressed ? 0.65 : 1,
          })}
        >
          <Icon name="FileText" color={c.blue} size={17} />
          <T variant="small" style={{ color: c.blue, flex: 1 }} numberOfLines={2}>
            {sourceName(source.fileName)} · PDF page {source.page}
          </T>
          <Icon name="ArrowRight" color={c.blue} size={16} />
        </Pressable>
      ))}
      <Pressable
        accessibilityRole="button"
        accessibilityState={{ expanded }}
        aria-expanded={expanded}
        onPress={() => setExpanded((value) => !value)}
        style={{ alignSelf: 'flex-start', minHeight: 44, justifyContent: 'center' }}
      >
        <T variant="small" style={{ color: c.muted }}>
          {expanded ? 'Hide supporting excerpts' : 'Show supporting excerpts'}
        </T>
      </Pressable>
      {expanded &&
        citations.map((source, index) => (
          <View
            key={source.chunkId + index}
            style={{ backgroundColor: c.canvas, padding: 14, borderRadius: 12, gap: 6 }}
          >
            <T variant="small" selectable>
              “{source.quote}”
            </T>
            <T variant="small" style={{ color: c.blue }}>
              PDF page {source.page}
            </T>
          </View>
        ))}
    </View>
  );
}
