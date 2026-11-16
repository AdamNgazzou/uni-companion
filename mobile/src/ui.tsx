import React, { useState } from 'react';
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  RefreshControl,
  StyleSheet,
  Text,
  TextInput,
  TextInputProps,
  TextProps,
  View,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useNavigation } from 'expo-router/react-navigation';
import { router } from 'expo-router';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import * as Haptics from 'expo-haptics';
import {
  ZoomIn,
  ZoomOut,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  ArrowDown,
  BookOpen,
  Bookmark,
  Check,
  CheckCheck,
  ChevronRight,
  ChevronDown,
  Code2,
  Cpu,
  Database,
  FileText,
  GraduationCap,
  HelpCircle,
  Mail,
  MessageCircle,
  MoreHorizontal,
  Plus,
  Search,
  Send,
  Settings,
  ShieldCheck,
  Upload,
  UserRound,
  X,
  Eye,
  EyeOff,
  LogOut,
  LockKeyhole,
  Pencil,
  SlidersHorizontal,
  History,
  Bell,
  Users,
  Sigma,
  Trash2,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Paperclip,
  Moon,
  Sun,
  Flag,
  Reply,
  RefreshCw,
  Sparkle,
} from 'lucide-react-native';
import { colors as c, fonts } from './theme';
import { Course, RootStack } from './types';
export const icons = {
  ZoomIn,
  ZoomOut,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  ArrowDown,
  BookOpen,
  Bookmark,
  Check,
  CheckCheck,
  ChevronRight,
  ChevronDown,
  Code2,
  Cpu,
  Database,
  FileText,
  GraduationCap,
  HelpCircle,
  Mail,
  MessageCircle,
  MoreHorizontal,
  Plus,
  Search,
  Send,
  Settings,
  ShieldCheck,
  Upload,
  UserRound,
  X,
  Eye,
  EyeOff,
  LogOut,
  LockKeyhole,
  Pencil,
  SlidersHorizontal,
  History,
  Bell,
  Users,
  Sigma,
  Trash2,
  CheckCircle2,
  AlertCircle,
  ExternalLink,
  Paperclip,
  Moon,
  Sun,
  Flag,
  Reply,
  RefreshCw,
  Sparkle,
};
export type IconName = keyof typeof icons;
export function Icon({
  name,
  size = 22,
  color = c.ink,
  fill,
}: {
  name: IconName;
  size?: number;
  color?: string;
  fill?: string;
}) {
  const Component = icons[name];
  return <Component size={size} strokeWidth={2.2} color={color} fill={fill ?? 'none'} />;
}
export function useNav() {
  return useNavigation<NativeStackNavigationProp<RootStack>>();
}
export function T({
  variant = 'body',
  style,
  ...props
}: TextProps & { variant?: 'body' | 'small' | 'label' | 'title' | 'heading' | 'hero' }) {
  return (
    <Text
      {...props}
      style={[
        s.text,
        {
          body: s.body,
          small: s.small,
          label: s.label,
          title: s.title,
          heading: s.heading,
          hero: s.hero,
        }[variant],
        style,
      ]}
    />
  );
}
export function IconButton({
  icon,
  onPress,
  label,
  color,
  selected = false,
  disabled = false,
}: {
  disabled?: boolean;
  icon: IconName;
  onPress: () => void;
  label: string;
  color?: string;
  selected?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected, disabled }}
      disabled={disabled}
      onPress={(event) => {
        event.stopPropagation();
        onPress();
      }}
      style={({ pressed }) => [
        s.iconButton,
        disabled && { opacity: 0.35 },
        pressed && { backgroundColor: c.blueSoft },
      ]}
    >
      <Icon name={icon} color={color ?? c.ink} fill={selected ? c.blueSoft : undefined} />
    </Pressable>
  );
}
export function Button({
  label,
  onPress,
  kind = 'primary',
  disabled = false,
  icon,
  busy = false,
}: {
  label: string;
  onPress: () => void;
  kind?: 'primary' | 'secondary' | 'quiet' | 'danger';
  disabled?: boolean;
  icon?: IconName;
  busy?: boolean;
}) {
  const primary = kind === 'primary';
  const ink = primary ? c.white : kind === 'danger' ? c.red : c.blue;
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: disabled || busy, busy }}
      disabled={disabled || busy}
      onPress={() => {
        if (Platform.OS !== 'web') void Haptics.selectionAsync().catch(() => {});
        onPress();
      }}
      style={({ pressed }) => [
        s.button,
        primary
          ? s.primary
          : kind === 'secondary'
            ? s.secondary
            : kind === 'danger'
              ? { backgroundColor: c.redSoft }
              : { backgroundColor: 'transparent', borderBottomWidth: 0 },
        pressed && { transform: [{ translateY: 2 }], borderBottomWidth: 1 },
        (disabled || busy) && { opacity: 0.45 },
      ]}
    >
      {busy ? (
        <ActivityIndicator color={ink} />
      ) : (
        <>
          <T variant="label" style={{ color: ink, textAlign: 'center' }}>
            {label}
          </T>
          {icon && <Icon name={icon} size={20} color={ink} />}
        </>
      )}
    </Pressable>
  );
}
export function Page({
  children,
  footer,
  scroll = true,
  padded = true,
  onRefresh,
  refreshing = false,
}: {
  children: React.ReactNode;
  footer?: React.ReactNode;
  scroll?: boolean;
  padded?: boolean;
  onRefresh?: () => void;
  refreshing?: boolean;
}) {
  return (
    <SafeAreaView style={s.page} edges={['top', 'left', 'right', 'bottom']}>
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        {scroll ? (
          <ScrollView
            refreshControl={
              onRefresh && Platform.OS !== 'web' ? (
                <RefreshControl
                  refreshing={refreshing}
                  onRefresh={onRefresh}
                  tintColor={c.blue}
                  colors={[c.blue]}
                />
              ) : undefined
            }
            keyboardShouldPersistTaps="handled"
            contentContainerStyle={[s.content, !padded && { padding: 0 }]}
            showsVerticalScrollIndicator={false}
          >
            {children}
          </ScrollView>
        ) : (
          <View style={[s.content, { flex: 1 }, !padded && { padding: 0 }]}>{children}</View>
        )}
        {footer && <View style={s.footer}>{footer}</View>}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
export function Header({
  title,
  back = true,
  right,
}: {
  title: string;
  back?: boolean;
  right?: React.ReactNode;
}) {
  return (
    <View style={s.header}>
      {back ? (
        <IconButton
          icon="ArrowLeft"
          label="Go back"
          onPress={() => (router.canGoBack() ? router.back() : router.replace('/'))}
        />
      ) : null}
      <T variant="title" style={{ flex: 1 }}>
        {title}
      </T>
      {right}
    </View>
  );
}
export function Logo() {
  return (
    <T variant="heading">
      <Text style={{ color: c.blue }}>Uni</Text> Companion<Text style={{ color: c.orange }}>.</Text>
    </T>
  );
}
export function Mascot({ size = 140 }: { size?: number }) {
  return (
    <Image
      source={require('../assets/mascot.png')}
      style={{ width: size, height: size }}
      resizeMode="contain"
      accessibilityLabel="Uni Companion's orange star holding a notebook"
    />
  );
}
export function Hero({
  title,
  subtitle,
  mascot = true,
}: {
  title: string;
  subtitle?: string;
  mascot?: boolean;
}) {
  return (
    <View style={s.heroRow}>
      <View style={{ flex: 1, gap: 10 }}>
        <T variant="hero">{title}</T>
        {subtitle && <T style={{ color: c.muted }}>{subtitle}</T>}
      </View>
      {mascot && <Mascot size={112} />}
    </View>
  );
}
export function Field({
  label,
  error,
  password,
  style,
  ...props
}: TextInputProps & { label: string; error?: string; password?: boolean }) {
  const [visible, setVisible] = useState(false);
  return (
    <View style={{ gap: 8 }}>
      <T variant="label">{label}</T>
      <View style={[s.inputWrap, error && { borderColor: c.red }]}>
        <TextInput
          {...props}
          accessibilityLabel={label}
          placeholderTextColor={c.muted}
          secureTextEntry={password && !visible}
          style={[s.input, style]}
        />
        {password && (
          <IconButton
            icon={visible ? 'EyeOff' : 'Eye'}
            label={visible ? 'Hide password' : 'Show password'}
            onPress={() => setVisible(!visible)}
            color={c.muted}
          />
        )}
      </View>
      {error && (
        <T variant="small" style={{ color: c.red }} accessibilityRole="alert">
          {error}
        </T>
      )}
    </View>
  );
}
export function SearchField({
  value,
  onChangeText,
  placeholder,
}: {
  value: string;
  onChangeText: (value: string) => void;
  placeholder: string;
}) {
  return (
    <View
      style={[s.inputWrap, { backgroundColor: c.canvas, borderWidth: 0, paddingHorizontal: 15 }]}
    >
      <Icon name="Search" color={c.muted} />
      <TextInput
        accessibilityLabel={placeholder}
        value={value}
        onChangeText={onChangeText}
        placeholder={placeholder}
        placeholderTextColor={c.muted}
        style={s.input}
      />
      {value ? <IconButton label="Clear search" icon="X" onPress={() => onChangeText('')} /> : null}
    </View>
  );
}
export function Notice({
  children,
  tone = 'info',
}: {
  children: React.ReactNode;
  tone?: 'info' | 'success' | 'error';
}) {
  const color = tone === 'success' ? c.green : tone === 'error' ? c.red : c.blue;
  return (
    <View
      style={[
        s.notice,
        {
          backgroundColor:
            tone === 'success' ? c.greenSoft : tone === 'error' ? c.redSoft : c.blueSoft,
        },
      ]}
    >
      <Icon name={tone === 'error' ? 'AlertCircle' : 'CheckCircle2'} size={20} color={color} />
      <T variant="small" style={{ color, flex: 1 }}>
        {children}
      </T>
    </View>
  );
}
export function Chip({
  label,
  selected,
  onPress,
  icon,
}: {
  label: string;
  selected?: boolean;
  onPress?: () => void;
  icon?: IconName;
}) {
  return (
    <Pressable
      disabled={!onPress}
      onPress={onPress}
      accessibilityRole={onPress ? 'button' : undefined}
      accessibilityState={{ selected }}
      style={[s.chip, selected && { backgroundColor: c.blue, borderColor: c.blue }]}
    >
      {icon && <Icon name={icon} size={16} color={selected ? c.white : c.blue} />}
      <T variant="small" style={{ color: selected ? c.white : c.ink, fontFamily: fonts.bold }}>
        {label}
      </T>
    </Pressable>
  );
}
export function CourseIcon({ course, size = 58 }: { course: Course; size?: number }) {
  const map: Record<Course['icon'], IconName> = {
    database: 'Database',
    code: 'Code2',
    cpu: 'Cpu',
    math: 'Sigma',
  };
  return (
    <View
      style={{
        width: size,
        height: size,
        borderRadius: 17,
        backgroundColor: course.color,
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Icon name={map[course.icon]} size={size * 0.53} color={c.blue} />
    </View>
  );
}
export function Badge({ course }: { course: Course }) {
  const curated = course.origin === 'admin';
  const recognized = course.recognised;
  const color = curated ? c.blue : recognized ? c.green : '#8B5616';
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4 }}>
      <Icon name={curated || recognized ? 'ShieldCheck' : 'UserRound'} size={13} color={color} />
      <T variant="small" style={{ fontSize: 11, color, fontFamily: fonts.bold }}>
        {curated
          ? 'Admin curated'
          : recognized
            ? 'Recognised · student-created'
            : 'Student-created'}
      </T>
    </View>
  );
}
export function CourseRow({
  course,
  onPress,
  selected,
}: {
  course: Course;
  onPress: () => void;
  selected?: boolean;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${course.code} ${course.title}`}
      accessibilityState={{ selected }}
      onPress={onPress}
      style={({ pressed }) => [
        s.courseRow,
        selected && { backgroundColor: c.blueSoft, borderColor: '#C6D7FF' },
        pressed && { opacity: 0.8 },
      ]}
    >
      <CourseIcon course={course} />
      <View style={{ flex: 1, gap: 3 }}>
        <T variant="small">{course.code}</T>
        <T variant="label">{course.title}</T>
        <Badge course={course} />
      </View>
      <Icon
        name={selected ? 'CheckCircle2' : 'ChevronRight'}
        color={selected ? c.blue : c.muted}
        size={21}
      />
    </Pressable>
  );
}
export function Row({
  title,
  detail,
  icon,
  onPress,
  right,
}: {
  title: string;
  detail?: string;
  icon: IconName;
  onPress?: () => void;
  right?: React.ReactNode;
}) {
  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : undefined}
      onPress={onPress}
      disabled={!onPress}
      style={({ pressed }) => [s.row, pressed && { backgroundColor: c.canvas }]}
    >
      <View style={s.rowIcon}>
        <Icon name={icon} color={c.blue} />
      </View>
      <View style={{ flex: 1, gap: 3 }}>
        <T variant="label">{title}</T>
        {detail && <T variant="small">{detail}</T>}
      </View>
      {right ?? (onPress && <Icon name="ChevronRight" color={c.muted} size={20} />)}
    </Pressable>
  );
}
export function Section({
  title,
  action,
  onPress,
}: {
  title: string;
  action?: string;
  onPress?: () => void;
}) {
  return (
    <View style={s.section}>
      <T variant="title" style={{ flex: 1 }}>
        {title}
      </T>
      {action && (
        <Pressable
          accessibilityRole="button"
          onPress={onPress}
          style={{ minHeight: 48, justifyContent: 'center' }}
        >
          <T variant="small" style={{ color: c.blue, fontFamily: fonts.bold }}>
            {action}
          </T>
        </Pressable>
      )}
    </View>
  );
}
export function Empty({
  title,
  body,
  action,
  onPress,
}: {
  title: string;
  body: string;
  action?: string;
  onPress?: () => void;
}) {
  return (
    <View style={{ alignItems: 'center', gap: 12, paddingVertical: 24 }}>
      <Mascot size={145} />
      <T variant="title" style={{ textAlign: 'center' }}>
        {title}
      </T>
      <T style={{ textAlign: 'center', color: c.muted }}>{body}</T>
      {action && onPress && <Button label={action} onPress={onPress} kind="secondary" />}
    </View>
  );
}
export const layout = StyleSheet.create({
  card: { borderRadius: 20, backgroundColor: c.blueSoft, padding: 20, gap: 12 },
  gap: { gap: 16 },
  inline: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  rule: { height: 1, backgroundColor: c.line },
  link: { color: c.blue, fontFamily: fonts.bold },
});
const s = StyleSheet.create({
  text: { color: c.ink, fontFamily: fonts.regular },
  body: { fontSize: 16, lineHeight: 24 },
  small: { fontSize: 13, lineHeight: 19, color: c.muted },
  label: { fontSize: 16, lineHeight: 23, fontFamily: fonts.bold },
  title: { fontSize: 21, lineHeight: 28, fontFamily: fonts.black },
  heading: { fontSize: 27, lineHeight: 35, fontFamily: fonts.black, letterSpacing: -0.8 },
  hero: { fontSize: 32, lineHeight: 36, letterSpacing: -0.7, fontFamily: fonts.black },
  page: { flex: 1, backgroundColor: c.white },
  content: { padding: 22, paddingTop: 8, paddingBottom: 26, gap: 18 },
  footer: {
    padding: 20,
    paddingTop: 14,
    gap: 10,
    borderTopWidth: 1,
    borderTopColor: c.line,
    backgroundColor: c.white,
  },
  header: { flexDirection: 'row', alignItems: 'center', gap: 6, minHeight: 56, marginLeft: -10 },
  iconButton: {
    minWidth: 48,
    minHeight: 48,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 14,
  },
  button: {
    minHeight: 56,
    borderRadius: 16,
    paddingVertical: 13,
    paddingHorizontal: 18,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
    borderBottomWidth: 4,
  },
  primary: { backgroundColor: c.blue, borderBottomColor: c.blueDark },
  secondary: { backgroundColor: c.blueSoft, borderBottomColor: '#CFDCFA' },
  heroRow: { flexDirection: 'row', alignItems: 'center', gap: 5, paddingVertical: 10 },
  inputWrap: {
    minHeight: 56,
    borderColor: '#DCE3EF',
    borderWidth: 1.5,
    borderRadius: 15,
    flexDirection: 'row',
    alignItems: 'center',
  },
  input: {
    fontFamily: fonts.regular,
    fontSize: 16,
    color: c.ink,
    padding: 15,
    flex: 1,
    minWidth: 0,
  },
  notice: { flexDirection: 'row', alignItems: 'flex-start', gap: 9, padding: 14, borderRadius: 14 },
  chip: {
    minHeight: 44,
    borderRadius: 13,
    paddingHorizontal: 14,
    paddingVertical: 11,
    borderColor: c.line,
    borderWidth: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 6,
  },
  courseRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 13,
    padding: 13,
    borderWidth: 1,
    borderColor: c.line,
    borderRadius: 19,
    minHeight: 94,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 13,
    paddingVertical: 17,
    borderBottomWidth: 1,
    borderBottomColor: c.line,
  },
  rowIcon: {
    width: 45,
    height: 45,
    backgroundColor: c.blueSoft,
    borderRadius: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  section: { flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 5, minHeight: 40 },
});
