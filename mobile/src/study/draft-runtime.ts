import AsyncStorage from '@react-native-async-storage/async-storage';
import { QuizDrafts } from './drafts';
// Only selected opaque IDs/position are saved, never credentials or quiz answer keys.
export const drafts = new QuizDrafts(AsyncStorage);
