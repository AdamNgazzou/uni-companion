import React from 'react';
import { Tabs } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors as c, fonts } from '../../theme';
import { Icon } from '../../ui';
export default function TabLayout() {
  const insets = useSafeAreaInsets();
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: c.blue,
        tabBarInactiveTintColor: c.muted,
        tabBarLabelStyle: { fontFamily: fonts.bold, fontSize: 12 },
        tabBarStyle: {
          height: 70 + insets.bottom,
          paddingTop: 10,
          paddingBottom: Math.max(9, insets.bottom),
          borderTopColor: c.line,
          elevation: 0,
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: 'Learn',
          tabBarIcon: ({ color }) => <Icon name="GraduationCap" color={String(color)} size={25} />,
        }}
      />
      <Tabs.Screen
        name="discuss"
        options={{
          title: 'Discuss',
          tabBarIcon: ({ color }) => <Icon name="MessageCircle" color={String(color)} size={25} />,
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'You',
          tabBarIcon: ({ color }) => <Icon name="UserRound" color={String(color)} size={25} />,
        }}
      />
    </Tabs>
  );
}
