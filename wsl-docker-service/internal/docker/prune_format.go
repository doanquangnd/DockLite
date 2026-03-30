package docker

import (
	"strings"

	"github.com/docker/docker/api/types"
	"github.com/docker/go-units"
)

func formatContainersPruneReport(r types.ContainersPruneReport) string {
	var b strings.Builder
	if len(r.ContainersDeleted) > 0 {
		b.WriteString("Deleted Containers:\n")
		for _, id := range r.ContainersDeleted {
			b.WriteString(id)
			b.WriteByte('\n')
		}
	}
	b.WriteString("Total reclaimed space: ")
	b.WriteString(units.HumanSize(float64(r.SpaceReclaimed)))
	b.WriteByte('\n')
	return b.String()
}

func formatImagesPruneReport(r types.ImagesPruneReport) string {
	var b strings.Builder
	if len(r.ImagesDeleted) > 0 {
		b.WriteString("Deleted Images:\n")
		for _, d := range r.ImagesDeleted {
			if d.Untagged != "" {
				b.WriteString("untagged: ")
				b.WriteString(d.Untagged)
				b.WriteByte('\n')
			}
			if d.Deleted != "" {
				b.WriteString("deleted: ")
				b.WriteString(d.Deleted)
				b.WriteByte('\n')
			}
		}
	}
	b.WriteString("Total reclaimed space: ")
	b.WriteString(units.HumanSize(float64(r.SpaceReclaimed)))
	b.WriteByte('\n')
	return b.String()
}

func formatNetworksPruneReport(r types.NetworksPruneReport) string {
	var b strings.Builder
	if len(r.NetworksDeleted) > 0 {
		b.WriteString("Deleted Networks:\n")
		for _, id := range r.NetworksDeleted {
			b.WriteString(id)
			b.WriteByte('\n')
		}
	}
	return b.String()
}

func formatVolumesPruneReport(r types.VolumesPruneReport) string {
	var b strings.Builder
	if len(r.VolumesDeleted) > 0 {
		b.WriteString("Deleted Volumes:\n")
		for _, v := range r.VolumesDeleted {
			b.WriteString(v)
			b.WriteByte('\n')
		}
	}
	b.WriteString("Total reclaimed space: ")
	b.WriteString(units.HumanSize(float64(r.SpaceReclaimed)))
	b.WriteByte('\n')
	return b.String()
}

// formatSystemPruneCombined ghép kết quả tương đương docker system prune (API không có một type SystemPruneReport).
func formatSystemPruneCombined(
	cont types.ContainersPruneReport,
	img types.ImagesPruneReport,
	net types.NetworksPruneReport,
	vol *types.VolumesPruneReport,
	bc *types.BuildCachePruneReport,
) string {
	total := cont.SpaceReclaimed + img.SpaceReclaimed
	if vol != nil {
		total += vol.SpaceReclaimed
	}
	if bc != nil {
		total += bc.SpaceReclaimed
	}
	var b strings.Builder
	if len(cont.ContainersDeleted) > 0 {
		b.WriteString("Deleted Containers:\n")
		for _, id := range cont.ContainersDeleted {
			b.WriteString(id)
			b.WriteByte('\n')
		}
	}
	if len(img.ImagesDeleted) > 0 {
		b.WriteString("Deleted Images:\n")
		for _, d := range img.ImagesDeleted {
			if d.Untagged != "" {
				b.WriteString("untagged: ")
				b.WriteString(d.Untagged)
				b.WriteByte('\n')
			}
			if d.Deleted != "" {
				b.WriteString("deleted: ")
				b.WriteString(d.Deleted)
				b.WriteByte('\n')
			}
		}
	}
	if len(net.NetworksDeleted) > 0 {
		b.WriteString("Deleted Networks:\n")
		for _, id := range net.NetworksDeleted {
			b.WriteString(id)
			b.WriteByte('\n')
		}
	}
	if vol != nil && len(vol.VolumesDeleted) > 0 {
		b.WriteString("Deleted Volumes:\n")
		for _, v := range vol.VolumesDeleted {
			b.WriteString(v)
			b.WriteByte('\n')
		}
	}
	if bc != nil && len(bc.CachesDeleted) > 0 {
		b.WriteString("Deleted build cache objects:\n")
		for _, id := range bc.CachesDeleted {
			b.WriteString(id)
			b.WriteByte('\n')
		}
	}
	b.WriteString("Total reclaimed space: ")
	b.WriteString(units.HumanSize(float64(total)))
	b.WriteByte('\n')
	return b.String()
}
