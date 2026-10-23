import type { CourseDetail, Material } from '../api/types';

export function resourceGroups(course: CourseDetail) {
  const groups: { id: string; title: string; materials: Material[] }[] = [];
  for (const material of course.materials) {
    const id = material.lessonId || 'other';
    let group = groups.find((item) => item.id === id);
    if (!group) {
      group = {
        id,
        title:
          course.structure.chapters?.find((chapter) => chapter.id === id)?.title ||
          'Other resources',
        materials: [],
      };
      groups.push(group);
    }
    group.materials.push(material);
  }
  return groups;
}

export function sourceScope(course: CourseDetail, materialId?: string, chapterId?: string) {
  return course.materials.filter((material) =>
    chapterId ? material.lessonId === chapterId : materialId ? material.id === materialId : true,
  );
}

export function resourceFormat(material: Material) {
  if (material.contentType !== 'application/pdf')
    return material.contentType === 'text/csv'
      ? 'CSV dataset · PDF study tools do not read this file yet'
      : 'Attachment · no in-app reader available yet';
  return `PDF${material.pageCount ? ` · ${material.pageCount} pages` : ''} · ${material.indexed ? 'Ready for AI study tools' : 'AI indexing pending'}`;
}
