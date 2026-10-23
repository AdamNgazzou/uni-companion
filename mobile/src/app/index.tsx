import React from 'react';
import { Redirect } from 'expo-router';
import { useSession } from '../auth/session';
export default function Index() {
  return <Redirect href={useSession().profile ? '/(tabs)' : '/auth'} />;
}
