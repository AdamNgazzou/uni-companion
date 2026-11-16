import AsyncStorage from '@react-native-async-storage/async-storage';
import { PracticeDrafts } from './drafts';
export const practiceDrafts = new PracticeDrafts(AsyncStorage);
