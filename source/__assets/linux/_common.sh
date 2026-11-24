#!/usr/bin/env bash
#
# Shared helpers for the Linux packers. The glibc sysroot lives in linux-x64-sysroot.sh.

# The newest <release> is what Flathub and the software centres show as the version, so it tracks
# ExifGlassVersion instead of being hand-bumped. A hand-written entry for this label (one carrying
# real release notes) is left alone.
# Usage: eg_sync_metainfo_release <metainfo.xml> <label> <release type, may be empty>
eg_sync_metainfo_release() {
	local file="$1" label="$2" release_type="$3"
	local top attr

	top="$(sed -n 's:.*<release version="\([^"]*\)".*:\1:p' "$file" | head -n 1)"
	if [[ "$top" == "$label" ]]; then
		echo "==> metainfo: newest <release> is already $label"
		return 0
	fi

	# AppStream knows stable, development and snapshot; a release type marks a pre-release
	attr=""
	[[ -n "$release_type" ]] && attr=' type="development"'

	echo "==> metainfo: inserting <release> $label (was ${top:-none})"
	awk -v label="$label" -v day="$(date +%F)" -v attr="$attr" '
		/<releases>/ && !inserted {
			print
			printf "    <release version=\"%s\" date=\"%s\"%s>\n", label, day, attr
			print  "      <url type=\"details\">https://github.com/d2phap/ExifGlass/releases</url>"
			print  "    </release>"
			inserted = 1
			next
		}
		{ print }
	' "$file" > "$file.part" && mv "$file.part" "$file"

	[[ "$(sed -n 's:.*<release version="\([^"]*\)".*:\1:p' "$file" | head -n 1)" == "$label" ]] \
		|| { echo "Error: could not insert <release> $label into $file (no <releases> element?)" >&2; return 1; }
}
