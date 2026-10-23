import AsyncStorage from '@react-native-async-storage/async-storage';
import { ComposerDrafts } from './drafts';
export const composerDrafts = new ComposerDrafts(AsyncStorage);
